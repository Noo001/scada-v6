// Copyright (c) Rapid Software LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scada.Lang;
using Scada.Web.Api;
using Scada.Web.Authorization;
using Scada.Web.Lang;
using Scada.Web.Plugins.PlgMimicEditor.Code;
using Scada.Web.Services;

namespace Scada.Web.Plugins.PlgMimicEditor.Controllers
{
    /// <summary>
    /// Represents a web API for managing mimic editor sessions.
    /// <para>Представляет веб-API для управления сеансами редактора мнемосхем.</para>
    /// </summary>
    /// <remarks>RatioT: REST-замена Razor-страниц MimicList и MimicOpen для нативного SPA.</remarks>
    [ApiController]
    [Route("Api/MimicEditor/Session/[action]")]
    [Authorize(Policy = PolicyName.Administrators)]
    [TypeFilter(typeof(MimicLockFilter))]
    [CamelCaseJsonFormatter]
    public class SessionApiController(
        IWebContext webContext,
        EditorManager editorManager) : ControllerBase
    {
        /// <summary>
        /// Gets the groups of open mimic diagrams.
        /// </summary>
        public Dto<object> GetMimicList()
        {
            try
            {
                return Dto<object>.Success(new
                {
                    Groups = editorManager.GetMimicGroups()
                        .Select(g => new
                        {
                            g.Name,
                            Mimics = g.GetMimics()
                                .Select(m => new
                                {
                                    m.FileName,
                                    MimicKey = m.MimicKey.ToString()
                                })
                                .ToList()
                        })
                        .ToList()
                });
            }
            catch (Exception ex)
            {
                webContext.Log.WriteError(ex.BuildErrorMessage(WebPhrases.ErrorInWebApi, nameof(GetMimicList)));
                return Dto<object>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Opens the mimic from the specified file and returns its editor key.
        /// </summary>
        [HttpPost]
        public Dto<object> OpenMimic([FromQuery] string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return Dto<object>.Fail("Path is required.");

                // RatioT: относительные пути разрешаются относительно каталога Views инстанса
                string fullPath = Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(webContext.AppDirs.InstanceDir, "Views", Scada.ScadaUtils.NormalPathSeparators(path));

                OpenResult openResult = editorManager.OpenMimic(fullPath);

                // RatioT: ключ сериализуем строкой — int64 теряет точность в JSON.number
                return openResult.IsSuccessful
                    ? Dto<object>.Success(new { MimicKey = openResult.MimicKey.ToString() })
                    : Dto<object>.Fail(openResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                webContext.Log.WriteError(ex.BuildErrorMessage(WebPhrases.ErrorInWebApi, nameof(OpenMimic)));
                return Dto<object>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Gets the list of mimic files in the instance Views directory.
        /// <para>Возвращает список файлов мнемосхем в каталоге Views инстанса.</para>
        /// </summary>
        public Dto<object> GetMimicFiles()
        {
            try
            {
                string viewsDir = Path.Combine(webContext.AppDirs.InstanceDir, "Views");
                List<string> files = [];

                if (Directory.Exists(viewsDir))
                {
                    foreach (string fileName in Directory.EnumerateFiles(viewsDir, "*.mim", SearchOption.AllDirectories))
                    {
                        files.Add(Scada.ScadaUtils.NormalPathSeparators(
                            Path.GetRelativePath(viewsDir, fileName)));
                    }
                }

                return Dto<object>.Success(new { Files = files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList() });
            }
            catch (Exception ex)
            {
                webContext.Log.WriteError(ex.BuildErrorMessage(WebPhrases.ErrorInWebApi, nameof(GetMimicFiles)));
                return Dto<object>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Saves and closes the mimic with the specified key.
        /// </summary>
        [HttpPost]
        public Dto CloseMimic([FromQuery] long key, [FromQuery] bool save)
        {
            try
            {
                if (save && !editorManager.SaveMimic(key, out string saveErrMsg))
                    return Dto.Fail(saveErrMsg);

                editorManager.CloseMimic(key);
                return Dto.Success();
            }
            catch (Exception ex)
            {
                webContext.Log.WriteError(ex.BuildErrorMessage(WebPhrases.ErrorInWebApi, nameof(CloseMimic)));
                return Dto.Fail(ex.Message);
            }
        }
    }
}
