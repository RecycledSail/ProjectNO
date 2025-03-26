using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Singleton instance
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (!_instance)
            {
                _instance = FindFirstObjectByType(typeof(GameManager)) as GameManager;
            }
            return _instance;
        }
    }

    // Users
    public List<User> users { get; private set; }
    public User player { get; private set; }
    public bool paused { get; private set; } = true;

    private void Awake()
    {
        if(_instance == null)
        {
            _instance = this;
        }
        else if(_instance != this)
        {
            Destroy(gameObject);
        }
        
        DontDestroyOnLoad(gameObject);
        
        
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        users = new List<User>();
        GameManager.Instance.StartNewGame("First");
        paused = false;

    }

    void StartNewGame(string nationCode)
    {
        int id = 0;
        foreach(string nationStr in GlobalVariables.NATIONS.Keys)
        {
            Nation nation = GlobalVariables.NATIONS[nationStr];
            foreach(string provinceStr in GlobalVariables.INITIAL_PROVINCES[nationStr])
            {
                Province province = GlobalVariables.PROVINCES[provinceStr];
                nation.AddProvinces(province);
            }
            User user = new User(id++, nation);
            if (nationStr == nationCode)
                player = user;
        }
    }

}
