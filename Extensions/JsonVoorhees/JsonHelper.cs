using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Extensions.JsonVoorhees
{
    public static class JsonHelper
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNameCaseInsensitive = true
        };
    }
}
