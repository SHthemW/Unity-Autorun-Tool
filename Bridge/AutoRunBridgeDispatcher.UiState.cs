using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed partial class AutoRunBridgeDispatcher
{
    private const int DefaultUiStateTimeoutMilliseconds = 10000;
    private const int MaximumUiStateTimeoutMilliseconds = 60000;
    private const int DefaultUiStatePollMilliseconds = 100;
    private const int MinimumUiStatePollMilliseconds = 16;
    private const int MaximumUiStatePollMilliseconds = 1000;

    private AutoRunUiStateWaitJob _uiStateWaitJob;

    private static AutoRunBridgeResponse GetUiState(
        AutoRunBridgeRequest request)
    {
        AutoRunUiStateQueryResult result = AutoRunUiStateService.Query(
            BuildUiStateQuery(request.payload));
        return AutoRunBridgeResponses.Success(
            request.id,
            $"Found {result.Total} matching UI state elements.",
            BuildUiStateData(result));
    }

    private bool StartUiStateWait(AutoRunBridgeJob job)
    {
        AutoRunBridgeRequest request = job.Request;
        AutoRunBridgePayload payload = request.payload
            ?? new AutoRunBridgePayload();
        if (_uiStateWaitJob != null)
        {
            job.Response = AutoRunBridgeResponses.Fail(
                request.id,
                "ui_state_wait_busy",
                "Another UI state wait request is still running.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(payload.query))
        {
            job.Response = AutoRunBridgeResponses.Fail(
                request.id,
                "ui_state_query_required",
                "query is required for wait_for_ui_state.");
            return true;
        }

        if (!AutoRunUiStateService.TryValidateComparison(
                payload.comparison,
                out string comparison,
                out string error))
        {
            job.Response = AutoRunBridgeResponses.Fail(
                request.id,
                "invalid_ui_state_comparison",
                error);
            return true;
        }

        int timeoutMilliseconds = Mathf.Clamp(
            payload.timeoutMilliseconds <= 0
                ? DefaultUiStateTimeoutMilliseconds
                : payload.timeoutMilliseconds,
            1,
            MaximumUiStateTimeoutMilliseconds);
        int pollMilliseconds = Mathf.Clamp(
            payload.pollMilliseconds <= 0
                ? DefaultUiStatePollMilliseconds
                : payload.pollMilliseconds,
            MinimumUiStatePollMilliseconds,
            MaximumUiStatePollMilliseconds);
        _uiStateWaitJob = new AutoRunUiStateWaitJob(
            job,
            BuildUiStateQuery(payload),
            string.IsNullOrWhiteSpace(payload.property)
                ? "value"
                : payload.property,
            comparison,
            payload.expected,
            timeoutMilliseconds,
            pollMilliseconds);
        return false;
    }

    private void PumpUiStateWait()
    {
        AutoRunUiStateWaitJob waitJob = _uiStateWaitJob;
        if (waitJob == null)
        {
            return;
        }

        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < waitJob.NextPollAt)
            {
                return;
            }

            waitJob.NextPollAt = now
                + waitJob.PollMilliseconds / 1000d;
            AutoRunUiStateQueryResult queryResult =
                AutoRunUiStateService.Query(waitJob.Query);
            AutoRunUiStateAssertionResult assertion =
                AutoRunUiStateService.Evaluate(
                    queryResult,
                    waitJob.Property,
                    waitJob.Comparison,
                    waitJob.Expected);
            long elapsedMilliseconds = (long)Math.Max(
                0,
                (EditorApplication.timeSinceStartup - waitJob.StartedAt) * 1000d);
            if (assertion.Matched)
            {
                AutoRunBridgeData data = BuildUiStateData(
                    queryResult,
                    assertion.MatchedElements);
                PopulateUiStateAssertionData(
                    data,
                    waitJob,
                    assertion,
                    elapsedMilliseconds);
                CompleteUiStateWait(AutoRunBridgeResponses.Success(
                    waitJob.Id,
                    $"UI state matched after {elapsedMilliseconds}ms.",
                    data));
                return;
            }

            if (elapsedMilliseconds < waitJob.TimeoutMilliseconds)
            {
                return;
            }

            AutoRunBridgeData timeoutData = BuildUiStateData(queryResult);
            PopulateUiStateAssertionData(
                timeoutData,
                waitJob,
                assertion,
                elapsedMilliseconds);
            CompleteUiStateWait(AutoRunBridgeResponses.Fail(
                waitJob.Id,
                "ui_state_wait_timeout",
                $"UI state did not match within {waitJob.TimeoutMilliseconds}ms."),
                timeoutData);
        }
        catch (Exception ex)
        {
            CompleteUiStateWait(AutoRunBridgeResponses.Fail(
                waitJob.Id,
                "ui_state_read_error",
                ex.Message));
        }
    }

    private void CompleteUiStateWait(
        AutoRunBridgeResponse response,
        AutoRunBridgeData data = null)
    {
        if (_uiStateWaitJob == null)
        {
            return;
        }

        if (data != null)
        {
            response.data = data;
        }

        AutoRunUiStateWaitJob waitJob = _uiStateWaitJob;
        _uiStateWaitJob = null;
        waitJob.Job.Response = response;
        PopulateRuntimeData(response);
        LogResponse(response);
        waitJob.Job.WaitHandle.Set();
    }

    private static AutoRunUiStateQuery BuildUiStateQuery(
        AutoRunBridgePayload payload)
    {
        payload = payload ?? new AutoRunBridgePayload();
        return new AutoRunUiStateQuery
        {
            Query = payload.query,
            Scope = payload.scope,
            Exact = payload.exact,
            IncludeInactive = payload.includeInactive,
            IncludeSensitive = payload.includeSensitive,
            Limit = payload.limit,
            Types = payload.types ?? new List<string>(),
        };
    }

    private static AutoRunBridgeData BuildUiStateData(
        AutoRunUiStateQueryResult result,
        List<AutoRunUiElementState> overrideElements = null)
    {
        List<AutoRunUiElementState> elements = overrideElements
            ?? result.Elements;
        return new AutoRunBridgeData
        {
            uiElements = elements,
            uiElementCount = result.Total,
            truncated = overrideElements == null && result.Truncated,
        };
    }

    private static void PopulateUiStateAssertionData(
        AutoRunBridgeData data,
        AutoRunUiStateWaitJob waitJob,
        AutoRunUiStateAssertionResult assertion,
        long elapsedMilliseconds)
    {
        data.uiStateMatched = assertion.Matched;
        data.uiStateProperty = waitJob.Property;
        data.uiStateComparison = waitJob.Comparison;
        data.uiStateExpected = waitJob.Expected;
        data.uiStateActual = assertion.ActualValue;
        data.elapsedMilliseconds = elapsedMilliseconds;
    }

    private sealed class AutoRunUiStateWaitJob
    {
        public AutoRunUiStateWaitJob(
            AutoRunBridgeJob job,
            AutoRunUiStateQuery query,
            string property,
            string comparison,
            string expected,
            int timeoutMilliseconds,
            int pollMilliseconds)
        {
            Job = job;
            Query = query;
            Property = property;
            Comparison = comparison;
            Expected = expected;
            TimeoutMilliseconds = timeoutMilliseconds;
            PollMilliseconds = pollMilliseconds;
            StartedAt = EditorApplication.timeSinceStartup;
            NextPollAt = StartedAt;
        }

        public AutoRunBridgeJob Job { get; }
        public string Id => Job.Request.id;
        public AutoRunUiStateQuery Query { get; }
        public string Property { get; }
        public string Comparison { get; }
        public string Expected { get; }
        public int TimeoutMilliseconds { get; }
        public int PollMilliseconds { get; }
        public double StartedAt { get; }
        public double NextPollAt { get; set; }
    }
}
