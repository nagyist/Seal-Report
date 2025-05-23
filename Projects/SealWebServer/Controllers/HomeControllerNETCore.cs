﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using Seal.Model;
using System.Linq;
using System.Threading.Tasks;
using Seal.Helpers;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.Security.Principal;
using System.Text;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using SealWebServer.Models.Configuration;

namespace SealWebServer.Controllers
{
    public partial class HomeController
    {
        private readonly string SessionIdKey = "SessionIdKey";
        public Authentication AuthenticationConfig { get; set; }

        private readonly IWebHostEnvironment _env;

        public HomeController(IWebHostEnvironment env, IOptions<Authentication> options)
        {
            _env = env;
            AuthenticationConfig = options.Value;
        }

        //Static sessions collection
        static Dictionary<string, Dictionary<string, object>> _sessions = new Dictionary<string, Dictionary<string, object>>();
        //Last request for a session to clear object after a timeout
        static Dictionary<string, DateTime> _sessionLastRequest = new Dictionary<string, DateTime>();

        string SessionKey
        {
            get
            {
                var key = HttpContext.Session.GetString(SessionIdKey);
                if (key == null)
                {
                    key = Guid.NewGuid().ToString();
                    HttpContext.Session.SetString(SessionIdKey, key);
                }
                return key;
            }
        }


        void SetSessionId(string sessionId)
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                ClearSessions();
                HttpContext.Session.SetString(SessionIdKey, sessionId);
            }
        }


        void ClearSessions()
        {
            try
            {
                var keys = _sessionLastRequest.Where(i => i.Value.AddMinutes(Startup.SessionTimeout) < DateTime.Now).ToList();

                foreach (var key in keys)
                {
                    //Session is over
                    if (_sessions[key.Key].ContainsKey(SessionUser))
                    {
                        var user = _sessions[key.Key][SessionUser] as SecurityUser;
                        if (user != null) user.Logout();
                    }

                    lock (_sessions)
                    {
                        _sessions.Remove(key.Key);
                    }
                    lock (_sessionLastRequest)
                    {
                        _sessionLastRequest.Remove(key.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.WriteLogException("ClearSessions", ex);
            }
        }

        void CheckSession()
        {
            if (!_sessions.ContainsKey(SessionKey))
            {
                lock (_sessions)
                {
                    _sessions.Add(SessionKey, new Dictionary<string, object>());
                }
                lock (_sessionLastRequest)
                {
                    _sessionLastRequest.Add(SessionKey, DateTime.Now);
                }
                //Clear old sessions
                ClearSessions();
            }
            _sessionLastRequest[SessionKey] = DateTime.Now;
        }

        void setSessionValue(string key, object value)
        {
            CheckSession();
            _sessions[SessionKey][key] = value;
        }

        void clearSessionValue(string key)
        {
            CheckSession();
            lock (_sessions)
            {
                if (_sessions[SessionKey].ContainsKey(key)) _sessions[SessionKey].Remove(key);
            }
        }

        object getSessionValue(string key)
        {

            CheckSession();
            return _sessions[SessionKey].ContainsKey(key) ? _sessions[SessionKey][key] : null;
        }

        private string getCookie(string name)
        {
            string result = "";
            if (Request.Cookies[name] != null)
            {
                result = Request.Cookies[name];
            }
            return result;
        }
        private void setCookie(string name, string value)
        {
            var options = new CookieOptions() { Expires = DateTime.Now.AddYears(2), HttpOnly = true };
            Response.Cookies.Append(name, value == null ? "" : value, options);
        }

        private ActionResult getFileResult(string path, Report report)
        {
            FileContentResult result;
            var bytes = System.IO.File.ReadAllBytes(path);
            if (Path.GetExtension(path) == ".htm" || Path.GetExtension(path) == ".html")
            {
                result = File(bytes, "text/html");
            }
            else
            {
                result = File(bytes, "application/force-download", path);
                if (report != null) result.FileDownloadName = Helper.CleanFileName(report.DisplayNameEx + Path.GetExtension(path));
                else result.FileDownloadName = Path.GetFileName(path);
            }

            return result;
        }

        private string RequestUrl
        {
            get
            {
                return Request.Path.Value;
            }
        }

        public static string GetWebUrl(HttpRequest request, HttpResponse response)
        {
            var appPath = request.PathBase.Value; ;
            return appPath + (appPath.EndsWith("/") ? "" : "/");
        }

        private string WebRootPath
        {
            get
            {
                return _env.WebRootPath;
            }
        }

        private string getContextDetail(HttpRequest request, SecurityUser user)
        {
            var result = new StringBuilder("\r\n");
            try
            {
                if (user != null) result.AppendFormat("User: '{0}'; Groups: '{1}'; \r\n", user.Name, user.SecurityGroupsDisplay);
                if (request != null)
                {
                    result.AppendFormat("URL: '{0}'\r\n", request.Path.Value);
                    result.AppendFormat("Session: '{0}'\r\n", SessionKey);
                }
            }
            catch (Exception ex)
            {
                Helper.WriteLogException($"getContextDetail", ex);
            }
            return result.ToString();
        }

        void writeDebug(string message)
        {
            if (Startup.DebugMode)
            {
                Debug.WriteLine(message);
                WebHelper.WriteLogEntryWebDebug(message, getContextDetail(Request, WebUser));
            }
        }

    }
}
