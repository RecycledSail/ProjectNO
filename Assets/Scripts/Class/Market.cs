using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * 마켓 설계 요약
 * - ProvinceMarket : 주(Province) 하나의 마켓 상태(제품별 재고/가격/최근 수요공급)를 가짐
 * - Market : 전체 월드의 마켓 관리자. 초기화, 생산/소비 처리, 가격 업데이트, 조회 API 제공
 * - 가격 공식(간단):  price_t = clamp( basePrice * ((demand+β)/(supply+β))^α  을 지수평활로 섞음 )
 *      - α(elasticity) : 수요/공급 변화에 대한 민감도
 *      - β(small)      : 0 분모/분자 보호 상수
 *      - smoothing     : 이전 가격과 새 가격을 혼합(급변 방지)
 *
 * 사용 예:
 *   Market.Init();                           // 시작 시 1회
 *   Market.Produce("Bebino", "Wheat", 100);  // 생산
 *   Market.Consume("Bebino", "Wheat", 30);   // 소비(가능한 만큼 소모, 실패 시 false)
 *   Market.UpdatePrices();                    // 턴 종료 시 1회
 *   int stock = Market.GetStock("Bebino", "Wheat");
 *   int price = Market.GetPrice("Bebino", "Wheat");
 */

public class Market
{
    // ===== 조정 파라미터 =====
    // 수요/공급에 대한 가격 탄력도 (크면 가격이 민감하게 반응)
    private const float ELASTICITY = 0.60f;
    // 0 나누기 보호용 작은 상수
    private const float EPS = 1.0f;
    // 가격 급등락 방지용 지수평활(0~1). 0.2면 새가격 20% + 구가격 80%.
    private const float SMOOTHING = 0.25f;
    // 가격 하한/상한 (기본가 대비 배수)
    private const float MIN_MULTIPLIER = 0.4f;
    private const float MAX_MULTIPLIER = 3.0f;

    // 주별 마켓(제품별 상태)이 들어감
    private static readonly Dictionary<string, ProvinceMarket> _marketsByProvince = new();

    // --------- 초기화 ----------
    // GlobalVariables.LoadDefaultData()가 끝난 뒤 한 번 호출하세요.
    public static void Init()
    {
        _marketsByProvince.Clear();

        // 모든 주에 대해 마켓 슬롯 생성
        foreach (var kv in GlobalVariables.PROVINCES)
        {
            var provinceName = kv.Key;
            var pm = new ProvinceMarket(provinceName);

            // 모든 제품에 대해 초기 상태 생성
            foreach (var p in GlobalVariables.PRODUCTS)
            {
                string product = p.Key;
                int basePrice = Mathf.Max(1, p.Value.InitialPrice); // 안전장치
                pm.CreateIfMissing(product, basePrice);
            }

            _marketsByProvince[provinceName] = pm;
        }

        Debug.Log($"Market initialized for {_marketsByProvince.Count} provinces and {GlobalVariables.PRODUCTS.Count} products.");
    }

    // --------- 생산/소비 ----------
    // 생산: 재고 증가 + 최근 공급량 기록
    public static void Produce(string provinceName, string product, int amount)
    {
        if (TryGetState(provinceName, product, out var state))
        {
            if (amount <= 0) return;
            state.Stock += amount;
            state.LastSupply += amount;
        }
    }

    // 소비: 재고에서 감소 + 최근 수요량 기록. 재고 부족 시 가능한 만큼만 소비하려면 allowPartial=true.
    public static bool Consume(string provinceName, string product, int amount, bool allowPartial = false)
    {
        if (!TryGetState(provinceName, product, out var state) || amount <= 0) return false;

        int consume = amount;
        if (state.Stock < amount)
        {
            if (!allowPartial) return false;
            consume = state.Stock;
        }

        state.Stock -= consume;
        state.LastDemand += consume;
        return consume > 0;
    }

    // --------- 가격 업데이트(턴 종료 시 호출) ----------
    public static void UpdatePrices()
    {
        foreach (var mk in _marketsByProvince.Values)
        {
            foreach (var st in mk.Products.Values)
            {
                // 기준가 확보 (GlobalVariables.Products의 InitialPrice를 기본가로 사용)
                int basePrice = GetBasePrice(st.ProductName);
                if (basePrice <= 0) basePrice = 1;

                // 간단 가격 공식: (수요+EPS)/(공급+EPS) 의 α승
                float ratio = (st.LastDemand + EPS) / (st.LastSupply + EPS);
                float targetPrice = basePrice * Mathf.Pow(ratio, ELASTICITY);

                // 재고 부족 벌칙(재고가 거의 없으면 추가 프리미엄)
                if (st.Stock <= 0) targetPrice *= 1.25f;
                else if (st.Stock < st.TargetStock) targetPrice *= Mathf.Lerp(1.0f, 1.15f, 1f - (float)st.Stock / Mathf.Max(1, st.TargetStock));

                // 하한/상한
                float minP = basePrice * MIN_MULTIPLIER;
                float maxP = basePrice * MAX_MULTIPLIER;
                targetPrice = Mathf.Clamp(targetPrice, minP, maxP);

                // 부드럽게 반영
                st.Price = Mathf.RoundToInt(st.Price * (1f - SMOOTHING) + targetPrice * SMOOTHING);
                if (st.Price <= 0) st.Price = 1;

                // 다음 턴 대비 수요/공급 카운터 리셋
                st.LastDemand = 0;
                st.LastSupply = 0;
            }
        }
    }

    // --------- 조회/도우미 ----------
    public static int GetStock(string provinceName, string product)
    {
        return TryGetState(provinceName, product, out var state) ? state.Stock : 0;
    }

    public static int GetPrice(string provinceName, string product)
    {
        return TryGetState(provinceName, product, out var state) ? state.Price : GetBasePrice(product);
    }

    public static bool TryGetState(string provinceName, string product, out ProductState state)
    {
        state = null;
        if (!_marketsByProvince.TryGetValue(provinceName, out var pm)) return false;

        if (!pm.Products.TryGetValue(product, out state))
        {
            // 해당 주에 이 제품 슬롯이 없다면 생성(지연 생성)
            int basePrice = GetBasePrice(product);
            state = pm.CreateIfMissing(product, basePrice);
        }
        return true;
    }

    private static int GetBasePrice(string product)
    {
        if (GlobalVariables.PRODUCTS.TryGetValue(product, out var prod))
            return Mathf.Max(1, prod.InitialPrice);
        return 1;
    }

    // 디버그 출력용
    public static void LogSnapshot(string provinceName)
    {
        if (!_marketsByProvince.TryGetValue(provinceName, out var pm))
        {
            Debug.LogWarning($"[Market] Province not found: {provinceName}");
            return;
        }

        Debug.Log($"--- Market Snapshot [{provinceName}] ---");
        foreach (var kv in pm.Products)
        {
            var s = kv.Value;
            Debug.Log($"{s.ProductName}: stock={s.Stock}, price={s.Price}, target={s.TargetStock}");
        }
    }
}

// ===== 주 단위 마켓 =====
[Serializable]
public class ProvinceMarket
{
    public string ProvinceName;
    public Dictionary<string, ProductState> Products = new();

    // 인구 기반 기본 목표 재고(원하면 인구, 생산 규모 등으로 조절)
    private int _defaultTargetStock;

    public ProvinceMarket(string provinceName)
    {
        ProvinceName = provinceName;

        // 대략적인 목표 재고: 인구/20 (원하는 방식으로 바꾸세요)
        if (GlobalVariables.PROVINCES.TryGetValue(provinceName, out var province))
        {
            int pop = (int)Math.Min((long)province.population, int.MaxValue); // 방어적 캐스팅
            _defaultTargetStock = Mathf.Max(50, pop / 20); // Mathf.Max(int,int)
        }
        else
        {
            _defaultTargetStock = 100;
        }

    }

    public ProductState CreateIfMissing(string product, int basePrice)
    {
        if (!Products.TryGetValue(product, out var s))
        {
            s = new ProductState(product, basePrice)
            {
                TargetStock = _defaultTargetStock
            };
            Products[product] = s;
        }
        return s;
    }
}

// ===== 제품별 상태(한 주 내) =====
[Serializable]
public class ProductState
{
    public string ProductName;
    public int Stock;        // 현재 재고
    public int Price;        // 현재 가격
    public int TargetStock;  // 목표 재고(부족시 가격 프리미엄에 사용)
    public int LastDemand;   // 최근 턴 소비량(가격 계산용)
    public int LastSupply;   // 최근 턴 생산량(가격 계산용)

    public ProductState(string name, int basePrice)
    {
        ProductName = name;
        Price = Math.Max(1, basePrice);
        Stock = 0;
        TargetStock = 100;
        LastDemand = 0;
        LastSupply = 0;
    }
}
