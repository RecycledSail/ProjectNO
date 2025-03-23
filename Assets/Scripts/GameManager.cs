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
    public List<User> users;
    public User player;
    public bool paused = true;

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
        GameManager.Instance.StartNewGame();
        
    }

    void StartNewGame()
    {
        users = new List<User>();

        Nation First = GlobalVariables.NATIONS["First"];
        Province Land = GlobalVariables.PROVINCES["Land"];
        First.AddProvinces(Land);
        First.balance = 53000;
        player = new User(1, First);
        paused = false;
    }

}
