using MCPForUnity.Editor;
using MCPForUnity.Editor.Services.Transport.Transports;
using UnityEditor;

// Enable the local editor connection after imports and script compilation finish.
[InitializeOnLoad]
internal static class UnityMcpLocalStartup
{
    static UnityMcpLocalStartup()
    {
        if (!AssetDatabase.IsAssetImportWorkerProcess())
            EditorApplication.update += StartWhenReady;
    }

    private static void StartWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        EditorApplication.update -= StartWhenReady;
        if (!StdioBridgeHost.IsRunning)
        {
            EditorPrefs.SetBool("MCPForUnity.TelemetryDisabled", true);
            McpCiBoot.StartStdioForCi();
        }
    }
}
