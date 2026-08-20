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
                                       IBuildingInvestor investor)
    {
        var reservation = new BuildingReservation(
            buildingType,
            targetProvince,
            investor);

        this.buildingReservations.Add(reservation);

    }

    public void RemoveFirstBuildingReservation(BuildingType buildingType,
                                               Province targetProvince)
    {
        int index = buildingReservations.FindIndex(reservation =>
            reservation.buildingType == buildingType &&
            reservation.targetProvince == targetProvince);

        if (index >= 0)
            buildingReservations.RemoveAt(index);
    }
}



public class BuildingReservation
{
    public BuildingType buildingType { get; set; }
    public Province targetProvince { get; set; }
    public IBuildingInvestor Investor { get; set; }  // ← 여기!

    public BuildingReservation(BuildingType buildingType,
                               Province targetProvince,
                               IBuildingInvestor investor = null)
    {
        this.buildingType   = buildingType;
        this.targetProvince = targetProvince;
        this.Investor       = investor;
    }
}


public interface IBuildingInvestor
{
    // 일부러 비워둠: "건물에 투자할 수 있는 존재"라는 표시만 하는 용도
}

public class BuildingInProgress
{ // 건축 진행 중인 빌딩을 나타내는 클래스
    // public float confirmindex { get; set; }
    public BuildingReservation Buildingrequest  { get; set; }  // ← 여기!

    public BuildingInProgress(
                               BuildingReservation buildingrequest = null)
    {
        // this.confirmindex   = confirmindex;
        this.Buildingrequest       = buildingrequest;
    }

    public void MatchingwithadjacentconstrucntionCompany()
    {
        // 일부러 비워둠: 추후 구현 예정
    }


}








