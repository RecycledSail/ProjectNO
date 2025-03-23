using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/**
 * HERE WE DEFINE CLASS
 **/
public enum Topography
{
    Plane,
    Mountain,
    Sea
}
public class Province
{
    public int id { get; }
    public String name { get;}
    public long population { get; set; }
    public Topography topo { get; }
    
    public Province(int id, String name, long population, Topography topo)
    {
        // this -> instance¿« id
        this.id = id;
        this.name = name;
        this.population = population;
        this.topo = topo;
    }

}

public class Nation
{
    public int id;
    public String name { get; }
    public List<Province> provinces { get; set; }
    public long balance { get; set; }
    public Nation(int id, String name)
    {
        this.id = id;
        this.name = name;
        provinces = new List<Province>();
    }
    public bool HasProvinces(Province province)
    {
        return provinces.Find(x => x.Equals(province)) != null;
    }

    public bool AddProvinces(Province province)
    {
        if (!HasProvinces(province))
        {
            provinces.Add(province);
            return true;
        }
        else return false;
    }

    public bool RemoveProvinces(Province province)
    {
        return provinces.Remove(province);
    }
    public long GetPopulation()
    {
        long sum = 0;
        foreach(Province province in provinces){
            sum += province.population;
        }
        return sum;
    }
}
public class User
{
    public int id { get; }
    public Nation nation { get; set; }
    public User(int id, Nation nation)
    {
        this.id = id;
        this.nation = nation;
    }
    public long GetCurrentBalance()
    {
        return this.nation.balance;
    }
}