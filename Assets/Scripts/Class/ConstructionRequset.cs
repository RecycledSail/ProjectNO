using System;
using System.Collections.Generic;
using System.Linq;


// 건축요청을 위한 클래스
public class ConstructionRequest
{
    public Nation requesterNation { get; set; }
    public Province targetProvince { get; set; }
    public List<BuildingReservation> buildingReservations { get; set; }

    public ConstructionRequest(Nation requesterNation)
    {
        this.requesterNation = requesterNation;
        this.buildingReservations = new List<BuildingReservation>();
    }
//투자자가 건설을 요청하는 메서드
    public void AddBuildingReservation(BuildingType buildingType,
                                       Province targetProvince,
                                       long quantity,
                                       IBuildingInvestor investor)
    {
        var reservation = new BuildingReservation(
            buildingType,
            targetProvince,
            quantity,
            investor);

        this.buildingReservations.Add(reservation);

    }
}



public class BuildingReservation
{
    public BuildingType buildingType { get; set; }
    public Province targetProvince { get; set; }
    public long quantity { get; set; }
    public IBuildingInvestor Investor { get; set; }  // ← 여기!

    public BuildingReservation(BuildingType buildingType,
                               Province targetProvince,
                               long quantity,
                               IBuildingInvestor investor = null)
    {
        this.buildingType   = buildingType;
        this.targetProvince = targetProvince;
        this.quantity       = quantity;
        this.Investor       = investor;
    }
}


public interface IBuildingInvestor
{
    // 일부러 비워둠: "건물에 투자할 수 있는 존재"라는 표시만 하는 용도
}










