import bpy


def _start():
    try:
        bpy.ops.preferences.addon_enable(module="blender-mcp-main")
    except Exception as e:
        print("MCP addon_enable:", e)
    try:
        bpy.context.scene.blendermcp_port = 9876
        bpy.ops.blendermcp.start_server()
        print("MCP server gestartet")
    except Exception as e:
        print("MCP start_server:", e)
    return None


bpy.app.timers.register(_start, first_interval=4.0)
