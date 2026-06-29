const fs = require("node:fs");
const path = require("node:path");

function loadNavMap(mapPath = process.env.UNITY_AUTORUN_NAV_MAP || "ui-nav-map.json") {
  const resolvedPath = path.resolve(process.cwd(), mapPath);
  const map = JSON.parse(fs.readFileSync(resolvedPath, "utf8"));
  return { map, path: resolvedPath };
}

function listRoutes(map) {
  return (map.routes || []).map(route => ({
    id: route.id,
    fromViewId: route.fromViewId,
    toViewId: route.toViewId,
    steps: (route.steps || []).length,
    autoRunSteps: safeAutoRunLength(map, route),
  }));
}

function safeAutoRunLength(map, route) {
  try {
    return resolveAutoRunSequence(map, route).length;
  } catch {
    return 0;
  }
}

function resolveRoute(map, options = {}) {
  const fromViewId = resolveViewId(map, options.from || options.fromViewId);
  const toViewId = resolveViewId(map, options.to || options.toViewId);
  let route = null;

  if (options.route || options.routeId) {
    route = (map.routes || []).find(item => item.id === (options.route || options.routeId));
  } else if (fromViewId && toViewId) {
    route = (map.routes || []).find(item => item.fromViewId === fromViewId && item.toViewId === toViewId);
  }

  if (!route && fromViewId && toViewId) {
    route = buildRouteFromTransitions(map, fromViewId, toViewId);
  }

  if (!route) {
    throw new Error("Route not found. Provide --route or both --from and --to.");
  }

  const autoRunSequence = resolveAutoRunSequence(map, route);
  if (autoRunSequence.length === 0) {
    throw new Error(`Route '${route.id}' has no AutoRun actions.`);
  }

  return {
    id: route.id,
    fromViewId: route.fromViewId,
    toViewId: route.toViewId,
    steps: route.steps || [],
    autoRunSequence,
  };
}

function resolveViewId(map, value) {
  if (!value) {
    return null;
  }

  const view = (map.views || []).find(item => item.id === value || item.name === value);
  return view ? view.id : value;
}

function resolveAutoRunSequence(map, route) {
  if (Array.isArray(route.autoRunSequence) && route.autoRunSequence.length > 0) {
    return route.autoRunSequence;
  }

  return (route.steps || []).map(step => {
    const control = findStepControl(map, step);
    if (!control || !control.autoRun) {
      throw new Error(`Step '${JSON.stringify(step)}' has no AutoRun action.`);
    }

    return control.autoRun;
  });
}

function findStepControl(map, step) {
  if (step.controlId) {
    return (map.controls || []).find(control => control.id === step.controlId);
  }

  if (step.transitionId) {
    const transition = (map.transitions || []).find(item => item.id === step.transitionId);
    return transition ? (map.controls || []).find(control => control.id === transition.controlId) : null;
  }

  return null;
}

function buildRouteFromTransitions(map, fromViewId, toViewId) {
  const transitions = map.transitions || [];
  const queue = [{ viewId: fromViewId, steps: [] }];
  const visited = new Set([fromViewId]);

  while (queue.length > 0) {
    const current = queue.shift();
    if (current.viewId === toViewId) {
      return {
        id: `computed.${fromViewId}.to.${toViewId}`,
        fromViewId,
        toViewId,
        steps: current.steps,
      };
    }

    for (const transition of transitions.filter(item => item.fromViewId === current.viewId)) {
      if (visited.has(transition.toViewId)) {
        continue;
      }

      visited.add(transition.toViewId);
      queue.push({
        viewId: transition.toViewId,
        steps: current.steps.concat({
          transitionId: transition.id,
          controlId: transition.controlId,
        }),
      });
    }
  }

  return null;
}

module.exports = {
  listRoutes,
  loadNavMap,
  resolveRoute,
};
