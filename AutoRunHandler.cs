using System;
using System.Collections.Generic;
using UnityEngine;

public class AutoRunHandler : MonoBehaviour
{
    [SerializeField]
    private List<AutoRunParam> _params;
    private Action<string> _msgHandler;

    public void Init(List<AutoRunParam> actionParams, Action<string> msgHandler)
    {
        if (_params != null)
        {
            Log("AutoRunHandler: AutoRunActions already set!");
        }
        _params = actionParams ?? throw new ArgumentNullException(nameof(actionParams));
        _msgHandler = msgHandler ?? throw new ArgumentNullException(nameof(msgHandler));

        Log($"AutoRunHandler is ready. {_params.Count} actions.");
    }

    private void Awake()
    {
        DontDestroyOnLoad(this);
    }

    private int _currentActionIndex = 0;
    private float _timer = 0f;

    private void Update()
    {
        if (_params == null)
            return;

        if (_currentActionIndex >= _params.Count)
        {
            Log("AutoRunHandler: All actions completed.");
            Destroy(gameObject);
            return;
        }

        var param = _params[_currentActionIndex];
        var action = new AutoRunAction(param);

        _timer += Time.deltaTime;
        if (_timer < param.delay)
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