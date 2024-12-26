using System;
using System.Collections.Generic;
using UnityEngine;

public class AutoRunHandler : MonoBehaviour
{
    [SerializeField]
    private List<AutoRunParam> _goActionParams;

    [SerializeField]
    private List<AutoRunParam> _stopActionParams;

    private Action _goActionCallback;
    private Action _stopActionCallback;
    private Action<string> _msgHandler;

    public void Init(
        List<AutoRunParam> goActionParams,
        List<AutoRunParam> stopActionParams = null,
        Action goActionCallback = null,
        Action stopActionCallback = null,
        Action<string> msgHandler = null
    )
    {
        _goActionParams = goActionParams ?? throw new ArgumentNullException(nameof(goActionParams));
        _stopActionParams = stopActionParams;

        _goActionCallback = goActionCallback;
        _stopActionCallback = stopActionCallback;

        _msgHandler = msgHandler;

        Log($"AutoRunHandler is ready. {_goActionParams.Count} go actions, {_stopActionParams.Count} stop actions.");
    }

    public void SetStatus(HandlerStatus status)
    {
        Log("Status setted to: " + status);

        _currentStatus = status;
        _currentActionIndex = 0;
        _timer = 0f;
    }

    public void Clear()
    {
        SetStatus(HandlerStatus.None);
        _goActionParams = null;
        _stopActionParams = null;
        _goActionCallback = null;
        _stopActionCallback = null;
        _msgHandler = null;
    }

    private void Awake()
    {
        DontDestroyOnLoad(this);
    }

    [SerializeField]
    private HandlerStatus _currentStatus = HandlerStatus.None;

    [SerializeField]
    private int _currentActionIndex = 0;

    [SerializeField]
    private float _timer = 0f;

    private void Update()
    {
        if (_currentStatus == HandlerStatus.None)
        {
            return;
        }

        var executingActionParams = _currentStatus switch
        {
            HandlerStatus.Go => _goActionParams,
            HandlerStatus.Stop => _stopActionParams,
            _ => throw new Exception("Unknown handler status: " + _currentStatus),
        };

        if (_currentActionIndex >= executingActionParams.Count)
        {
            var callback = _currentStatus switch
            {
                HandlerStatus.Go => _goActionCallback,
                HandlerStatus.Stop => _stopActionCallback,
                _ => throw new Exception("Unknown handler status: " + _currentStatus),
            };

            callback?.Invoke();
            SetStatus(HandlerStatus.None);

            Log("AutoRunHandler: All actions completed.");
            return;
        }

        var param = executingActionParams[_currentActionIndex];
        var action = new AutoRunAction(param);

        _timer += Time.deltaTime;
        if (_timer < param.delay)
        {
            return;
        }

        var msg = action.Execute();
        Log(msg);

        _currentActionIndex++;
        _timer = 0f;
    }

    private void Log(string msg)
    {
        _msgHandler?.Invoke(msg);
    }
}