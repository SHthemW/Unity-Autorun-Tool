using System;
using System.Collections.Generic;
using UnityEngine;

public class AutoRunHandler : MonoBehaviour
{
    [SerializeField]
    private List<AutoRunAction> _autoActions;
    private Action<string> _msgHandler;

    public void Init(List<AutoRunAction> autoActions, Action<string> msgHandler)
    {
        if (_autoActions != null)
        {
            Log("AutoRunHandler: AutoRunActions already set!");
        }
        _autoActions = autoActions ?? throw new ArgumentNullException(nameof(autoActions));
        _msgHandler = msgHandler ?? throw new ArgumentNullException(nameof(msgHandler));

        Log($"AutoRunHandler is ready. {_autoActions.Count} actions.");
    }

    private void Awake()
    {
        DontDestroyOnLoad(this);
    }

    private int _currentActionIndex = 0;
    private float _timer = 0f;

    private void Update()
    {
        if (_autoActions == null)
            return;

        if (_currentActionIndex >= _autoActions.Count)
        {
            Log("AutoRunHandler: All actions completed.");
            Destroy(gameObject);
            return;
        }

        var action = _autoActions[_currentActionIndex];

        _timer += Time.deltaTime;
        if (_timer < action.delay)
            return;

        var msg = action.Execute();
        Log(msg);

        _currentActionIndex++;
        _timer = 0f;
    }

    private void Log(string msg)
    {
        if (string.IsNullOrEmpty(msg))
            return;

        if (_msgHandler != null)
            _msgHandler.Invoke(msg);
        else
            Debug.Log(msg);
    }
}