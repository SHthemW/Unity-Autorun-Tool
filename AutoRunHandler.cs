using System;
using System.Collections.Generic;
using UnityEngine;


public class AutoRunHandler : MonoBehaviour
{
    private List<AutoRunAction> _autoActions;
    private Action<string> _msgHandler;

    public void Init(List<AutoRunAction> autoActions, Action<string> msgHandler)
    {
        if (_autoActions != null)
        {
            Debug.LogWarning("AutoRunHandler: AutoRunActions already set!");
        }
        _autoActions = autoActions ?? throw new ArgumentNullException(nameof(autoActions));
        _msgHandler = msgHandler ?? throw new ArgumentNullException(nameof(msgHandler));

        _msgHandler.Invoke("\nAutoRunHandler is ready.");
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
            return;

        var action = _autoActions[_currentActionIndex];

        _timer += Time.deltaTime;
        if (_timer < action.delay)
            return;

        var msg = action.Execute();
        if (msg != null)
            _msgHandler.Invoke(msg);

        _currentActionIndex++;
        _timer = 0f;
    }
}