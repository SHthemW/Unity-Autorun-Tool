const tools = [
  {
    name: "unity_status",
    description: "Check the Unity AutoRun bridge status.",
    inputSchema: { type: "object", properties: {} },
  },
  {
    name: "unity_play",
    description: "Request Unity Editor to enter Play Mode.",
    inputSchema: { type: "object", properties: {} },
  },
  {
    name: "unity_stop",
    description: "Request Unity Editor to exit Play Mode.",
    inputSchema: { type: "object", properties: {} },
  },
  {
    name: "list_buttons",
    description: "List current Unity UI buttons.",
    inputSchema: {
      type: "object",
      properties: {
        framework: { type: "string", enum: ["ugui", "fairygui", "all"] },
      },
    },
  },
  {
    name: "click_button",
    description: "Click a Unity UI button by name or text.",
    inputSchema: {
      type: "object",
      properties: {
        name: { type: "string" },
        text: { type: "string" },
        framework: { type: "string", enum: ["ugui", "fairygui"] },
      },
      required: ["name"],
    },
  },
  {
    name: "run_sequence",
    description: "Run a sequence of AutoRun button actions.",
    inputSchema: {
      type: "object",
      properties: {
        actions: { type: "array", items: { type: "object" } },
      },
      required: ["actions"],
    },
  },
  {
    name: "list_ui_routes",
    description: "List routes in a UI navigation map file.",
    inputSchema: {
      type: "object",
      properties: {
        mapPath: { type: "string" },
      },
    },
  },
  {
    name: "resolve_ui_route",
    description: "Resolve a UI route from a navigation map without executing it.",
    inputSchema: {
      type: "object",
      properties: {
        mapPath: { type: "string" },
        route: { type: "string" },
        from: { type: "string" },
        to: { type: "string" },
      },
    },
  },
  {
    name: "run_ui_route",
    description: "Resolve a UI route and execute it through AutoRun.",
    inputSchema: {
      type: "object",
      properties: {
        mapPath: { type: "string" },
        route: { type: "string" },
        from: { type: "string" },
        to: { type: "string" },
      },
    },
  },
];

module.exports = {
  tools,
};
