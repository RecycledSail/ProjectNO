# Province ColorMap 제작 가이드

`SelectProvince`는 Unity Terrain 위에서 마우스가 가리키는 프로빈스를 ColorMap으로 찾습니다.
이 텍스처는 지형 표현용 텍스처가 아니라 프로빈스 식별용 ID 맵입니다. 각 프로빈스는 하나의 고유하고 균일한 RGBA 색을 사용해야 합니다.

## ColorMap 만들기

1. Gaea Terrain의 XZ 영역과 같은 정사각형 비율 및 방향으로 캔버스를 만듭니다.
2. 각 프로빈스를 빈틈없이 채운 도형으로 그립니다. 경계에는 안티앨리어싱, 블러, 페더, 반투명 효과를 사용하지 않습니다.
3. 모든 프로빈스에 서로 다른 색을 지정합니다. 각 RGB 값과 대응하는 `Provinces.json`의 `name`을 기록합니다.
4. 바다, 선택할 수 없는 땅, 맵 바깥 영역은 완전 투명하게 만듭니다. (`alpha = 0`)
5. 손실 없는 PNG로 내보냅니다. 예: `Assets/Textures/Maps/ProvinceColorMap.png`

## Unity Import 설정

내보낸 PNG를 선택한 뒤 Inspector에서 아래처럼 설정합니다.

- Texture Type: `Default`
- Read/Write: 활성화
- sRGB: 비활성화
- Alpha Is Transparency: 비활성화
- Generate Mip Maps: 비활성화
- Filter Mode: `Point (no filter)`
- Compression: `None`

`SelectProvince`는 픽셀의 정확한 색을 읽습니다. JPEG, 압축, 안티앨리어싱, Mip Map은 원래 색을 바꿔 프로빈스 매핑이 실패할 수 있습니다.

## 씬에 연결하기

1. `PlayScene`의 활성 Gaea Unity Terrain 오브젝트를 선택합니다.
2. 오브젝트의 `SelectProvince` 컴포넌트를 확인합니다.
3. PNG를 `Color Map` 필드에 넣습니다.
4. `Province Colors`에 프로빈스마다 한 항목씩 추가합니다. `Province Name`은 `Assets/Resources/Provinces.json`의 이름과 정확히 같아야 하며, `Color`는 PNG에서 사용한 채우기 색과 정확히 같아야 합니다.
5. 클릭 시 프로빈스가 상하 반전되어 선택되면 `Flip Color Map Y`를 활성화합니다.

## 현재 범위

현재 `SelectProvince`는 호버와 클릭으로 프로빈스를 찾고, 프로빈스 상세 UI를 열며, 기존 길 건설 모드에서 도로를 토글합니다. 이후 지형 표시 기능이 연결될 수 있도록 호버, 선택, 화면 모드 이벤트도 제공합니다.

기존 메시 방식은 프로빈스마다 별도의 `MeshRenderer`가 있어 국가색을 칠하고 메시 경계를 따라 테두리를 그릴 수 있었습니다. 하나의 Unity Terrain에는 프로빈스별 Renderer나 메시 경계가 없으므로, 국가색, 도로색, 호버 테두리를 화면에 표시하려면 같은 ColorMap을 읽는 Terrain 오버레이 셰이더 또는 별도의 투명 오버레이 메시가 필요합니다. 이 표시용 시스템은 ColorMap을 직접 수정하지 않고 동일한 프로빈스 색 매핑을 사용해야 합니다.
