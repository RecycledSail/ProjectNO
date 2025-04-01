using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


/// <summary>
/// Topography Enum
/// ����, ��, �ٴ� ����
/// </summary>
public enum Topography
{
    Plane,
    Mountain,
    Sea
}

/// <summary>
/// ���κ� Ŭ����
/// ID, �̸�, �α�, ��������, ���� �÷� ����
/// </summary>
public class Province
{
    public int id { get; }
    public string name { get;}
    public long population { get; set; }
    public Topography topo { get; }
    public Color32 color { get; }
    public Nation nation { get; set; } = null;

    /// <summary>
    /// Province �ʱ�ȭ
    /// </summary>
    /// <param name="id">���κ��� ID</param>
    /// <param name="name">���κ��� �̸�</param>
    /// <param name="population">���κ��� �α� ��</param>
    /// <param name="topo">���κ��� ��������</param>
    /// <param name="color">���κ��� ����</param>
    public Province(int id, string name, long population, Topography topo, Color32 color)
    {
        // this -> instance�� id
        this.id = id;
        this.name = name;
        this.population = population;
        this.topo = topo;
        this.color = color;
    }

    public void AddNation(Nation nation)
    {
        this.nation = nation;
    }

    public bool RemoveNation(Nation nation)
    {
        if (this.nation == nation)
        {
            this.nation = null;
            return true;
        }
        else return false;
    }
}

/// <summary>
/// ���� Ŭ����
/// ID, �̸�, ���� ���κ�, ��� ����
/// </summary>
public class Nation
{
    public int id;
    public string name { get; }
    public List<Province> provinces { get; set; }
    public long balance { get; set; }

    /// <summary>
    /// ���� ������
    /// </summary>
    /// <param name="id">������ ID</param>
    /// <param name="name">������ �̸�(�ڵ�)</param>
    public Nation(int id, string name)
    {
        this.id = id;
        this.name = name;
        provinces = new List<Province>();
    }

    /// <summary>
    /// �ش� ���κ󽺰� �ִ��� �˻��ϴ� �޼���
    /// </summary>
    /// <param name="province">�˻��ϰ��� �ϴ� ���κ�</param>
    /// <returns>���κ� ���� ���̸� true, �ƴϸ� false</returns>
    public bool HasProvinces(Province province)
    {
        return provinces.Find(x => x.Equals(province)) != null;
    }

    /// <summary>
    /// ���κ󽺸� �߰��ϴ� �޼���
    /// �߰� �õ� �� ���� ���ο� ���� boolean ��ȯ
    /// </summary>
    /// <param name="province">�߰��ϰ��� �ϴ� ���κ�</param>
    /// <returns>�߰� �����ϸ� true, �ƴϸ� false</returns>
    public bool AddProvinces(Province province)
    {
        if (!HasProvinces(province))
        {
            provinces.Add(province);
            province.AddNation(this);
            return true;
        }
        else return false;
    }

    /// <summary>
    /// ���κ󽺸� �����ϴ� �޼���
    /// ���� �õ� �� ���� ���ο� ���� boolean ��ȯ
    /// </summary>
    /// <param name="province">�����ϰ��� �ϴ� ���κ�</param>
    /// <returns>���� �����ϸ� true, �ƴϸ� false</returns>
    public bool RemoveProvinces(Province province)
    {
        bool avail = provinces.Remove(province);
        if (avail)
        {
            return province.RemoveNation(this);
        }
        else return false;
    }

    /// <summary>
    /// ������ ��� ���κ��� �α��� ���� ��ȯ
    /// </summary>
    /// <returns>������ ��� ���κ��� �α� ��</returns>
    public long GetPopulation()
    {
        long sum = 0;
        foreach(Province province in provinces){
            sum += province.population;
        }
        return sum;
    }
}

/// <summary>
/// ���� Ŭ����
/// ���� ID, ������ ����
/// </summary>
public class User
{
    public int id { get; }
    public Nation nation { get; set; }

    /// <summary>
    /// ���� ������
    /// </summary>
    /// <param name="id">������ ID</param>
    /// <param name="nation">������ ���� ����</param>
    public User(int id, Nation nation)
    {
        this.id = id;
        this.nation = nation;
    }

    /// <summary>
    /// ������ ���� ������ ��� ��ȯ
    /// </summary>
    /// <returns>������ ���� ������ ���</returns>
    public long GetCurrentBalance()
    {
        return this.nation.balance;
    }
}