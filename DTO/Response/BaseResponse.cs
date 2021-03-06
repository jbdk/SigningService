using System;
using System.Collections.Generic;
using System.Reflection;

namespace Outercurve.DTO.Response
{
    public class BaseResponse
    {
        public BaseResponse()
        {
            Version = Assembly.GetExecutingAssembly().GetName().Version;
        }

        public List<string> Warnings { get; set; }

        public List<string> Errors { get; set; }

        public Version Version { get; private set; }
    }
}