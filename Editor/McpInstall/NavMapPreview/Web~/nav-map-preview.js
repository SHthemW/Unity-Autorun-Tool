(function () {
  "use strict";

  var dataElement = document.getElementById("nav-map-data");
  if (!dataElement) {
    return;
  }

  var model;
  try {
    model = JSON.parse(dataElement.textContent || "{}");
  } catch (error) {
    showBootstrapError("The navigation data embedded in this preview is invalid.", error);
    return;
  }

  model.views = Array.isArray(model.views) ? model.views : [];
  model.edges = Array.isArray(model.edges) ? model.edges : [];

  var ui = {
    sourcePath: document.getElementById("source-path"),
    statViews: document.getElementById("stat-views"),
    statTransitions: document.getElementById("stat-transitions"),
    statConnected: document.getElementById("stat-connected"),
    statUnconnected: document.getElementById("stat-unconnected"),
    graphHost: document.getElementById("graph-host"),
    graphStatus: document.getElementById("graph-status"),
    graphStatusText: document.getElementById("graph-status-text"),
    viewSearch: document.getElementById("view-search"),
    searchSection: document.getElementById("search-section"),
    searchCount: document.getElementById("search-count"),
    searchResults: document.getElementById("search-results"),
    selectionDetails: document.getElementById("selection-details"),
    unconnectedCount: document.getElementById("unconnected-count"),
    unconnectedViews: document.getElementById("unconnected-views"),
    fitGraph: document.getElementById("fit-graph"),
    zoomIn: document.getElementById("zoom-in"),
    zoomOut: document.getElementById("zoom-out"),
    clearSelection: document.getElementById("clear-selection"),
    edgeLabelToggle: document.getElementById("edge-label-toggle"),
    zoomLevel: document.getElementById("zoom-level"),
    tooltip: document.getElementById("nav-tooltip")
  };

  var state = {
    svg: null,
    originalViewBox: null,
    currentViewBox: null,
    pan: null,
    panMoved: false,
    suppressNextBackgroundClick: false,
    selectedViewId: null,
    selectedEdgeId: null,
    searchResults: []
  };

  var viewById = new Map();
  var edgeById = new Map();
  var edgesByViewId = new Map();
  var nodeElementByViewId = new Map();
  var edgeElementById = new Map();

  buildIndices();
  populateHeader();
  renderUnconnectedViews();
  renderEmptyDetails();
  bindToolbar();
  renderGraph();

  function buildIndices() {
    model.views.forEach(function (view) {
      viewById.set(view.id, view);
      edgesByViewId.set(view.id, []);
    });

    model.edges.forEach(function (edge) {
      edgeById.set(edge.domId, edge);
      if (edgesByViewId.has(edge.fromViewId)) {
        edgesByViewId.get(edge.fromViewId).push(edge);
      }
      if (edge.toViewId !== edge.fromViewId && edgesByViewId.has(edge.toViewId)) {
        edgesByViewId.get(edge.toViewId).push(edge);
      }
    });
  }

  function populateHeader() {
    ui.sourcePath.textContent = model.sourcePath || "Unknown source";
    ui.sourcePath.title = model.sourcePath || "";
    ui.statViews.textContent = String(model.views.length);
    ui.statTransitions.textContent = String(numberOrZero(model.transitionCount));
    ui.statConnected.textContent = String(numberOrZero(model.connectedViewCount));
    ui.statUnconnected.textContent = String(numberOrZero(model.unconnectedViewCount));
    ui.unconnectedCount.textContent = String(numberOrZero(model.unconnectedViewCount));
  }

  function bindToolbar() {
    ui.viewSearch.addEventListener("input", renderSearchResults);
    ui.viewSearch.addEventListener("keydown", function (event) {
      if (event.key === "Enter" && state.searchResults.length > 0) {
        event.preventDefault();
        selectView(state.searchResults[0].id, true);
      } else if (event.key === "Escape") {
        event.preventDefault();
        ui.viewSearch.value = "";
        renderSearchResults();
        ui.viewSearch.blur();
      }
    });

    ui.fitGraph.addEventListener("click", fitGraph);
    ui.zoomIn.addEventListener("click", function () {
      zoomFromCenter(0.76);
    });
    ui.zoomOut.addEventListener("click", function () {
      zoomFromCenter(1.32);
    });
    ui.clearSelection.addEventListener("click", function () {
      clearSelection();
      renderEmptyDetails();
    });
    ui.edgeLabelToggle.addEventListener("change", function () {
      ui.graphHost.classList.toggle("show-edge-labels", ui.edgeLabelToggle.checked);
    });

    document.addEventListener("keydown", function (event) {
      var target = event.target;
      var isTyping =
        target instanceof HTMLInputElement ||
        target instanceof HTMLTextAreaElement ||
        target instanceof HTMLSelectElement;
      if (event.key === "/" && !isTyping) {
        event.preventDefault();
        ui.viewSearch.focus();
        ui.viewSearch.select();
        return;
      }

      if (isTyping) {
        return;
      }

      if (event.key === "+" || event.key === "=") {
        event.preventDefault();
        zoomFromCenter(0.76);
      } else if (event.key === "-") {
        event.preventDefault();
        zoomFromCenter(1.32);
      } else if (event.key === "0") {
        event.preventDefault();
        fitGraph();
      } else if (event.key === "Escape") {
        clearSelection();
        renderEmptyDetails();
      }
    });
  }

  async function renderGraph() {
    var connectedViews = model.views.filter(function (view) {
      return view.connected;
    });

    if (connectedViews.length === 0 || model.edges.length === 0) {
      setGraphStatus(
        "empty",
        "No valid transitions were found. Use the unconnected view list to inspect discovered views."
      );
      ui.zoomLevel.textContent = "—";
      return;
    }

    if (!window.Viz || typeof window.Viz.instance !== "function") {
      setGraphStatus(
        "error",
        "Viz.js could not be loaded. Reinstall the tool or restore the preview web assets."
      );
      return;
    }

    setGraphStatus("loading", "Laying out navigation graph...");
    try {
      var viz = await window.Viz.instance();
      var svg = viz.renderSVGElement(buildGraphvizModel(connectedViews), {
        engine: "dot"
      });
      prepareSvg(svg);
      ui.graphHost.appendChild(svg);
      bindGraphElements();
      bindViewportEvents();
      hideGraphStatus();
      fitGraph();
    } catch (error) {
      setGraphStatus("error", "Graphviz failed to render this navigation map.");
      console.error("Navigation map render failed", error);
    }
  }

  function buildGraphvizModel(connectedViews) {
    return {
      directed: true,
      graphAttributes: {
        rankdir: "LR",
        bgcolor: "transparent",
        pad: "0.32",
        nodesep: "0.42",
        ranksep: "0.86",
        splines: "spline",
        outputorder: "edgesfirst",
        pack: "24",
        packmode: "array_t",
        newrank: "true",
        remincross: "true",
        mclimit: "2",
        fontname: "Arial"
      },
      nodeAttributes: {
        shape: "box",
        style: "rounded,filled",
        color: "#78a5c9",
        fillcolor: "#eef6ff",
        fontcolor: "#1f2937",
        fontname: "Arial",
        fontsize: "11",
        penwidth: "1.35",
        margin: "0.16,0.11"
      },
      edgeAttributes: {
        color: "#7b8797",
        fontcolor: "#374151",
        fontname: "Arial",
        fontsize: "9",
        arrowsize: "0.68",
        penwidth: "1.15"
      },
      nodes: connectedViews.map(function (view) {
        return {
          name: view.id,
          attributes: {
            id: view.domId,
            label:
              truncate(displayName(view), 28) +
              "\\n" +
              truncate(view.id || "", 40)
          }
        };
      }),
      edges: model.edges.map(function (edge) {
        return {
          tail: edge.fromViewId,
          head: edge.toViewId,
          attributes: {
            id: edge.domId,
            label: edge.label || "transition"
          }
        };
      })
    };
  }

  function prepareSvg(svg) {
    svg.removeAttribute("width");
    svg.removeAttribute("height");
    svg.setAttribute("width", "100%");
    svg.setAttribute("height", "100%");
    svg.setAttribute("preserveAspectRatio", "xMidYMid meet");
    svg.setAttribute("role", "img");
    svg.setAttribute("aria-label", "Directed navigation graph");
    svg.style.touchAction = "none";

    var viewBox = svg.viewBox && svg.viewBox.baseVal;
    if (!viewBox || !viewBox.width || !viewBox.height) {
      throw new Error("Graphviz returned an SVG without a usable viewBox.");
    }

    state.svg = svg;
    state.originalViewBox = {
      x: viewBox.x,
      y: viewBox.y,
      width: viewBox.width,
      height: viewBox.height
    };
    state.currentViewBox = copyViewBox(state.originalViewBox);
  }

  function bindGraphElements() {
    model.views.forEach(function (view) {
      if (!view.connected) {
        return;
      }

      var element = document.getElementById(view.domId);
      if (!element || !state.svg.contains(element)) {
        return;
      }

      nodeElementByViewId.set(view.id, element);
      makeGraphElementAccessible(
        element,
        "View " + displayName(view) + ", " + view.id
      );
      removeDirectTitle(element);
      element.addEventListener("pointerenter", function (event) {
        showViewTooltip(view, event);
      });
      element.addEventListener("pointermove", positionTooltip);
      element.addEventListener("pointerleave", hideTooltip);
      element.addEventListener("click", function (event) {
        event.stopPropagation();
        selectView(view.id, false);
      });
      element.addEventListener("keydown", function (event) {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          selectView(view.id, true);
        }
      });
    });

    model.edges.forEach(function (edge) {
      var element = document.getElementById(edge.domId);
      if (!element || !state.svg.contains(element)) {
        return;
      }

      edgeElementById.set(edge.domId, element);
      makeGraphElementAccessible(element, edgeAriaLabel(edge));
      removeDirectTitle(element);
      element.addEventListener("pointerenter", function (event) {
        showEdgeTooltip(edge, event);
      });
      element.addEventListener("pointermove", positionTooltip);
      element.addEventListener("pointerleave", hideTooltip);
      element.addEventListener("click", function (event) {
        event.stopPropagation();
        selectEdge(edge.domId, false);
      });
      element.addEventListener("keydown", function (event) {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          selectEdge(edge.domId, true);
        }
      });
    });
  }

  function makeGraphElementAccessible(element, label) {
    element.setAttribute("tabindex", "0");
    element.setAttribute("role", "button");
    element.setAttribute("aria-label", label);
  }

  function removeDirectTitle(element) {
    var firstChild = element.firstElementChild;
    if (firstChild && firstChild.tagName.toLowerCase() === "title") {
      firstChild.remove();
    }
  }

  function bindViewportEvents() {
    state.svg.addEventListener(
      "wheel",
      function (event) {
        event.preventDefault();
        var factor = Math.exp(Math.max(-500, Math.min(500, event.deltaY)) * 0.0012);
        zoomAt(event.clientX, event.clientY, factor);
      },
      { passive: false }
    );

    state.svg.addEventListener("pointerdown", function (event) {
      if (event.button !== 0 || closestGraphItem(event.target)) {
        return;
      }

      state.pan = {
        pointerId: event.pointerId,
        clientX: event.clientX,
        clientY: event.clientY,
        viewBox: copyViewBox(state.currentViewBox)
      };
      state.panMoved = false;
      ui.graphHost.classList.add("is-panning");
      state.svg.setPointerCapture(event.pointerId);
    });

    state.svg.addEventListener("pointermove", function (event) {
      if (!state.pan || state.pan.pointerId !== event.pointerId) {
        return;
      }

      var rect = state.svg.getBoundingClientRect();
      if (!rect.width || !rect.height) {
        return;
      }

      var deltaX =
        ((event.clientX - state.pan.clientX) / rect.width) *
        state.pan.viewBox.width;
      var deltaY =
        ((event.clientY - state.pan.clientY) / rect.height) *
        state.pan.viewBox.height;
      if (Math.abs(deltaX) > 1 || Math.abs(deltaY) > 1) {
        state.panMoved = true;
      }

      setViewBox({
        x: state.pan.viewBox.x - deltaX,
        y: state.pan.viewBox.y - deltaY,
        width: state.pan.viewBox.width,
        height: state.pan.viewBox.height
      });
    });

    function endPan(event) {
      if (!state.pan || state.pan.pointerId !== event.pointerId) {
        return;
      }

      state.suppressNextBackgroundClick = state.panMoved;
      if (state.svg.hasPointerCapture(event.pointerId)) {
        state.svg.releasePointerCapture(event.pointerId);
      }
      state.pan = null;
      ui.graphHost.classList.remove("is-panning");
    }

    state.svg.addEventListener("pointerup", endPan);
    state.svg.addEventListener("pointercancel", endPan);
    state.svg.addEventListener("click", function (event) {
      if (state.suppressNextBackgroundClick) {
        state.suppressNextBackgroundClick = false;
        return;
      }
      if (!closestGraphItem(event.target)) {
        clearSelection();
        renderEmptyDetails();
      }
    });
    state.svg.addEventListener("dblclick", function (event) {
      if (!closestGraphItem(event.target)) {
        fitGraph();
      }
    });
  }

  function closestGraphItem(target) {
    return target instanceof Element ? target.closest(".node, .edge") : null;
  }

  function renderUnconnectedViews() {
    var views = model.views
      .filter(function (view) {
        return !view.connected;
      })
      .sort(compareViews);
    var fragment = document.createDocumentFragment();

    if (views.length === 0) {
      fragment.appendChild(
        createElement("div", "empty-details", "Every discovered view has at least one transition.")
      );
    } else {
      views.forEach(function (view) {
        fragment.appendChild(createViewListButton(view, false));
      });
    }

    ui.unconnectedViews.replaceChildren(fragment);
  }

  function renderSearchResults() {
    var query = normalize(ui.viewSearch.value);
    if (!query) {
      state.searchResults = [];
      ui.searchSection.hidden = true;
      ui.searchResults.replaceChildren();
      ui.searchCount.textContent = "0";
      return;
    }

    state.searchResults = model.views
      .map(function (view) {
        return {
          view: view,
          score: searchScore(view, query)
        };
      })
      .filter(function (result) {
        return result.score < 100;
      })
      .sort(function (left, right) {
        if (left.score !== right.score) {
          return left.score - right.score;
        }
        if (left.view.connected !== right.view.connected) {
          return left.view.connected ? -1 : 1;
        }
        return compareViews(left.view, right.view);
      })
      .slice(0, 30)
      .map(function (result) {
        return result.view;
      });

    var fragment = document.createDocumentFragment();
    if (state.searchResults.length === 0) {
      fragment.appendChild(
        createElement("div", "empty-details", "No matching views.")
      );
    } else {
      state.searchResults.forEach(function (view) {
        fragment.appendChild(createViewListButton(view, true));
      });
    }

    ui.searchCount.textContent = String(state.searchResults.length);
    ui.searchResults.replaceChildren(fragment);
    ui.searchSection.hidden = false;
  }

  function createViewListButton(view, showStatus) {
    var button = createElement("button", "list-item");
    button.type = "button";
    button.dataset.listViewId = view.id;
    button.appendChild(createElement("span", "list-primary", displayName(view)));
    button.appendChild(createElement("span", "list-secondary", view.id));
    if (showStatus) {
      button.appendChild(
        createElement(
          "span",
          "list-status" + (view.connected ? " is-connected" : ""),
          view.connected ? "Connected" : "Unconnected"
        )
      );
    }
    button.addEventListener("click", function () {
      selectView(view.id, true);
    });
    return button;
  }

  function selectView(viewId, shouldFocus) {
    var view = viewById.get(viewId);
    if (!view) {
      return;
    }

    resetGraphFocus();
    state.selectedViewId = viewId;
    state.selectedEdgeId = null;
    markSelectedListItems(viewId);

    var nodeElement = nodeElementByViewId.get(viewId);
    if (nodeElement) {
      ui.graphHost.classList.add("is-focused");
      nodeElement.classList.add("is-selected");
      var incidentEdges = edgesByViewId.get(viewId) || [];
      var focusElements = [nodeElement];
      incidentEdges.forEach(function (edge) {
        var edgeElement = edgeElementById.get(edge.domId);
        if (edgeElement) {
          edgeElement.classList.add("is-context");
          focusElements.push(edgeElement);
        }

        var neighborId =
          edge.fromViewId === viewId ? edge.toViewId : edge.fromViewId;
        var neighborElement = nodeElementByViewId.get(neighborId);
        if (neighborElement && neighborId !== viewId) {
          neighborElement.classList.add("is-neighbor");
          focusElements.push(neighborElement);
        }
      });

      if (shouldFocus) {
        focusGraphElements(focusElements);
      }
    }

    renderViewDetails(view);
  }

  function selectEdge(edgeId, shouldFocus) {
    var edge = edgeById.get(edgeId);
    if (!edge) {
      return;
    }

    resetGraphFocus();
    state.selectedViewId = null;
    state.selectedEdgeId = edgeId;
    markSelectedListItems(null);
    ui.graphHost.classList.add("is-focused");

    var edgeElement = edgeElementById.get(edgeId);
    if (edgeElement) {
      edgeElement.classList.add("is-active");
    }

    var fromElement = nodeElementByViewId.get(edge.fromViewId);
    var toElement = nodeElementByViewId.get(edge.toViewId);
    if (fromElement) {
      fromElement.classList.add("is-neighbor");
    }
    if (toElement) {
      toElement.classList.add("is-neighbor");
    }
    if (shouldFocus) {
      focusGraphElements([edgeElement, fromElement, toElement]);
    }

    renderEdgeDetails(edge);
  }

  function clearSelection() {
    state.selectedViewId = null;
    state.selectedEdgeId = null;
    resetGraphFocus();
    markSelectedListItems(null);
    hideTooltip();
  }

  function resetGraphFocus() {
    ui.graphHost.classList.remove("is-focused");
    nodeElementByViewId.forEach(function (element) {
      element.classList.remove("is-selected", "is-neighbor");
    });
    edgeElementById.forEach(function (element) {
      element.classList.remove("is-active", "is-context");
    });
  }

  function markSelectedListItems(viewId) {
    document.querySelectorAll("[data-list-view-id]").forEach(function (element) {
      element.classList.toggle("is-selected", element.dataset.listViewId === viewId);
    });
  }

  function renderEmptyDetails() {
    var container = createElement("div", "empty-details");
    container.appendChild(
      createElement(
        "p",
        "",
        "Select a view or route to highlight only its immediate navigation context."
      )
    );
    container.appendChild(
      createElement(
        "p",
        "",
        "Edge labels appear on hover or selection and can be enabled globally from the toolbar."
      )
    );
    if (numberOrZero(model.invalidTransitionCount) > 0) {
      container.appendChild(
        createElement(
          "p",
          "warning-copy",
          model.invalidTransitionCount +
            " transition(s) reference a missing view and were excluded."
        )
      );
    }
    ui.selectionDetails.replaceChildren(container);
  }

  function renderViewDetails(view) {
    var container = document.createDocumentFragment();
    container.appendChild(createElement("h3", "detail-title", displayName(view)));
    container.appendChild(createElement("code", "detail-code", view.id));

    var incidentEdges = edgesByViewId.get(view.id) || [];
    var incoming = incidentEdges.filter(function (edge) {
      return edge.toViewId === view.id;
    });
    var outgoing = incidentEdges.filter(function (edge) {
      return edge.fromViewId === view.id;
    });
    var definitionList = createElement("dl", "detail-grid");
    appendDefinition(definitionList, "Status", view.connected ? "Connected" : "Unconnected");
    appendDefinition(definitionList, "Incoming", String(incoming.length));
    appendDefinition(definitionList, "Outgoing", String(outgoing.length));
    container.appendChild(definitionList);

    container.appendChild(createElement("div", "detail-subheading", "Prefab path"));
    container.appendChild(
      createElement(
        "code",
        "detail-path",
        view.prefabPath || "No prefab path recorded"
      )
    );

    if (outgoing.length > 0) {
      container.appendChild(createElement("div", "detail-subheading", "Outgoing routes"));
      container.appendChild(createRouteList(outgoing, view.id, true));
    }
    if (incoming.length > 0) {
      container.appendChild(createElement("div", "detail-subheading", "Incoming routes"));
      container.appendChild(createRouteList(incoming, view.id, false));
    }
    if (incidentEdges.length === 0) {
      container.appendChild(
        createElement(
          "p",
          "empty-details",
          "This view has no valid incoming or outgoing transition."
        )
      );
    }

    ui.selectionDetails.replaceChildren(container);
  }

  function createRouteList(edges, selectedViewId, outgoing) {
    var list = createElement("div", "route-list");
    edges.forEach(function (edge) {
      var otherId = outgoing ? edge.toViewId : edge.fromViewId;
      var otherView = viewById.get(otherId);
      var button = createElement(
        "button",
        "route-button",
        (outgoing ? "→ " : "← ") + displayName(otherView)
      );
      button.type = "button";
      var transitionSummary =
        edge.transitionCount +
        " transition" +
        (edge.transitionCount === 1 ? "" : "s");
      if (edge.label && edge.label !== transitionSummary) {
        transitionSummary += " · " + edge.label;
      }
      button.appendChild(
        createElement("small", "", transitionSummary)
      );
      button.addEventListener("click", function () {
        selectEdge(edge.domId, true);
      });
      list.appendChild(button);
    });
    return list;
  }

  function renderEdgeDetails(edge) {
    var fromView = viewById.get(edge.fromViewId);
    var toView = viewById.get(edge.toViewId);
    var container = document.createDocumentFragment();
    container.appendChild(
      createElement(
        "h3",
        "detail-title",
        displayName(fromView) + " → " + displayName(toView)
      )
    );
    container.appendChild(
      createElement(
        "code",
        "detail-code",
        edge.fromViewId + " → " + edge.toViewId
      )
    );

    var endpointRow = createElement("div", "endpoint-row");
    endpointRow.appendChild(createEndpointButton(fromView));
    endpointRow.appendChild(createElement("span", "endpoint-arrow", "→"));
    endpointRow.appendChild(createEndpointButton(toView));
    container.appendChild(endpointRow);

    container.appendChild(
      createElement(
        "div",
        "detail-subheading",
        edge.transitionCount +
          " transition" +
          (edge.transitionCount === 1 ? "" : "s")
      )
    );

    var transitions = Array.isArray(edge.transitions) ? edge.transitions : [];
    transitions.forEach(function (transition) {
      var card = createElement("div", "transition-card");
      card.appendChild(
        createElement(
          "div",
          "transition-label",
          transition.controlId || transition.label || "transition"
        )
      );
      card.appendChild(
        createElement(
          "div",
          "transition-kind",
          "Kind: " + (transition.kind || "unspecified")
        )
      );
      container.appendChild(card);
    });

    ui.selectionDetails.replaceChildren(container);
  }

  function createEndpointButton(view) {
    var button = createElement("button", "endpoint-button", displayName(view));
    button.type = "button";
    button.title = view ? view.id : "";
    button.addEventListener("click", function () {
      if (view) {
        selectView(view.id, true);
      }
    });
    return button;
  }

  function appendDefinition(list, term, value) {
    list.appendChild(createElement("dt", "", term));
    list.appendChild(createElement("dd", "", value));
  }

  function showViewTooltip(view, event) {
    showTooltip(
      displayName(view),
      [view.id, view.prefabPath || "No prefab path recorded"],
      event
    );
  }

  function showEdgeTooltip(edge, event) {
    var fromView = viewById.get(edge.fromViewId);
    var toView = viewById.get(edge.toViewId);
    var transitions = Array.isArray(edge.transitions) ? edge.transitions : [];
    var lines = transitions.slice(0, 5).map(function (transition) {
      return transition.controlId || transition.label || transition.kind || "transition";
    });
    if (transitions.length > 5) {
      lines.push("+" + (transitions.length - 5) + " more");
    }
    showTooltip(
      displayName(fromView) + " → " + displayName(toView),
      lines,
      event
    );
  }

  function showTooltip(title, lines, event) {
    var fragment = document.createDocumentFragment();
    fragment.appendChild(createElement("div", "tooltip-title", title));
    lines.forEach(function (line) {
      fragment.appendChild(createElement("div", "tooltip-line", line));
    });
    ui.tooltip.replaceChildren(fragment);
    ui.tooltip.hidden = false;
    positionTooltip(event);
  }

  function positionTooltip(event) {
    if (ui.tooltip.hidden) {
      return;
    }

    var gap = 14;
    var left = event.clientX + gap;
    var top = event.clientY + gap;
    var width = ui.tooltip.offsetWidth;
    var height = ui.tooltip.offsetHeight;
    if (left + width > window.innerWidth - 8) {
      left = event.clientX - width - gap;
    }
    if (top + height > window.innerHeight - 8) {
      top = event.clientY - height - gap;
    }
    ui.tooltip.style.left = Math.max(8, left) + "px";
    ui.tooltip.style.top = Math.max(8, top) + "px";
  }

  function hideTooltip() {
    ui.tooltip.hidden = true;
  }

  function fitGraph() {
    if (!state.svg || !state.originalViewBox) {
      return;
    }
    setViewBox(copyViewBox(state.originalViewBox));
  }

  function zoomFromCenter(factor) {
    if (!state.svg) {
      return;
    }
    var rect = state.svg.getBoundingClientRect();
    zoomAt(rect.left + rect.width / 2, rect.top + rect.height / 2, factor);
  }

  function zoomAt(clientX, clientY, factor) {
    if (!state.svg || !state.currentViewBox || !state.originalViewBox) {
      return;
    }

    var current = state.currentViewBox;
    var minimumWidth = state.originalViewBox.width * 0.025;
    var maximumWidth = state.originalViewBox.width * 6;
    var width = clamp(current.width * factor, minimumWidth, maximumWidth);
    var scale = width / current.width;
    var height = current.height * scale;
    var point = clientToSvgPoint(clientX, clientY);
    var ratioX = (point.x - current.x) / current.width;
    var ratioY = (point.y - current.y) / current.height;

    setViewBox({
      x: point.x - ratioX * width,
      y: point.y - ratioY * height,
      width: width,
      height: height
    });
  }

  function clientToSvgPoint(clientX, clientY) {
    var point = state.svg.createSVGPoint();
    point.x = clientX;
    point.y = clientY;
    var matrix = state.svg.getScreenCTM();
    if (matrix) {
      return point.matrixTransform(matrix.inverse());
    }

    var rect = state.svg.getBoundingClientRect();
    return {
      x:
        state.currentViewBox.x +
        ((clientX - rect.left) / rect.width) * state.currentViewBox.width,
      y:
        state.currentViewBox.y +
        ((clientY - rect.top) / rect.height) * state.currentViewBox.height
    };
  }

  function focusGraphElements(elements) {
    if (!state.svg || !state.originalViewBox || !Array.isArray(elements)) {
      return;
    }

    var bounds = null;
    elements.forEach(function (element) {
      if (!element) {
        return;
      }

      var clientBox = element.getBoundingClientRect();
      if (!clientBox.width || !clientBox.height) {
        return;
      }

      if (!bounds) {
        bounds = {
          left: clientBox.left,
          top: clientBox.top,
          right: clientBox.right,
          bottom: clientBox.bottom
        };
        return;
      }

      bounds.left = Math.min(bounds.left, clientBox.left);
      bounds.top = Math.min(bounds.top, clientBox.top);
      bounds.right = Math.max(bounds.right, clientBox.right);
      bounds.bottom = Math.max(bounds.bottom, clientBox.bottom);
    });
    if (!bounds) {
      return;
    }

    var firstCorner = clientToSvgPoint(bounds.left, bounds.top);
    var secondCorner = clientToSvgPoint(bounds.right, bounds.bottom);
    var box = {
      x: Math.min(firstCorner.x, secondCorner.x),
      y: Math.min(firstCorner.y, secondCorner.y),
      width: Math.abs(secondCorner.x - firstCorner.x),
      height: Math.abs(secondCorner.y - firstCorner.y)
    };
    var hostRect = ui.graphHost.getBoundingClientRect();
    var aspect = hostRect.width / Math.max(1, hostRect.height);
    var width = Math.max(600, box.width * 1.28);
    var height = Math.max(360, box.height * 1.28);
    if (width / height < aspect) {
      width = height * aspect;
    } else {
      height = width / aspect;
    }
    width = Math.min(width, state.originalViewBox.width);
    height = Math.min(height, state.originalViewBox.height);

    setViewBox({
      x: box.x + box.width / 2 - width / 2,
      y: box.y + box.height / 2 - height / 2,
      width: width,
      height: height
    });
  }

  function setViewBox(viewBox) {
    state.currentViewBox = copyViewBox(viewBox);
    state.svg.setAttribute(
      "viewBox",
      [
        round(viewBox.x),
        round(viewBox.y),
        round(viewBox.width),
        round(viewBox.height)
      ].join(" ")
    );
    updateZoomLevel();
  }

  function updateZoomLevel() {
    if (!state.originalViewBox || !state.currentViewBox) {
      ui.zoomLevel.textContent = "—";
      return;
    }
    var percent = Math.round(
      (state.originalViewBox.width / state.currentViewBox.width) * 100
    );
    ui.zoomLevel.textContent = percent + "%";
  }

  function setGraphStatus(kind, message) {
    ui.graphStatus.hidden = false;
    ui.graphStatus.className =
      "graph-status" +
      (kind === "loading"
        ? " is-loading"
        : kind === "error"
          ? " is-error"
          : "");
    ui.graphStatusText.textContent = message;
  }

  function hideGraphStatus() {
    ui.graphStatus.hidden = true;
  }

  function showBootstrapError(message, error) {
    var status = document.getElementById("graph-status");
    var statusText = document.getElementById("graph-status-text");
    if (status) {
      status.hidden = false;
      status.className = "graph-status is-error";
    }
    if (statusText) {
      statusText.textContent = message;
    }
    console.error(message, error);
  }

  function edgeAriaLabel(edge) {
    return (
      "Route from " +
      edge.fromViewId +
      " to " +
      edge.toViewId +
      ", " +
      edge.transitionCount +
      " transition" +
      (edge.transitionCount === 1 ? "" : "s")
    );
  }

  function searchScore(view, query) {
    var name = normalize(view.name);
    var id = normalize(view.id);
    var path = normalize(view.prefabPath);
    if (id === query || name === query) {
      return 0;
    }
    if (name.indexOf(query) === 0) {
      return 1;
    }
    if (id.indexOf(query) === 0) {
      return 2;
    }
    if (name.indexOf(query) >= 0) {
      return 3;
    }
    if (id.indexOf(query) >= 0) {
      return 4;
    }
    if (path.indexOf(query) >= 0) {
      return 5;
    }
    return 100;
  }

  function compareViews(left, right) {
    return displayName(left).localeCompare(displayName(right), undefined, {
      sensitivity: "base",
      numeric: true
    });
  }

  function displayName(view) {
    if (!view) {
      return "Unknown view";
    }
    return view.name || view.id || "Unnamed view";
  }

  function normalize(value) {
    return String(value || "").trim().toLocaleLowerCase();
  }

  function truncate(value, maxLength) {
    value = String(value || "");
    return value.length <= maxLength
      ? value
      : value.slice(0, Math.max(0, maxLength - 3)) + "...";
  }

  function createElement(tagName, className, text) {
    var element = document.createElement(tagName);
    if (className) {
      element.className = className;
    }
    if (text !== undefined && text !== null) {
      element.textContent = String(text);
    }
    return element;
  }

  function copyViewBox(viewBox) {
    return {
      x: viewBox.x,
      y: viewBox.y,
      width: viewBox.width,
      height: viewBox.height
    };
  }

  function numberOrZero(value) {
    var number = Number(value);
    return Number.isFinite(number) ? number : 0;
  }

  function clamp(value, minimum, maximum) {
    return Math.max(minimum, Math.min(maximum, value));
  }

  function round(value) {
    return Math.round(value * 1000) / 1000;
  }
})();
