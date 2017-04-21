using Restup.HttpMessage.Models.Schemas;
using Restup.Webserver.Models.Schemas;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Restup.WebServer.Models.Schemas
{
    internal class InternalServerErrorResponse : RestResponse
    {
        public object ContentData { get; set; }

        public InternalServerErrorResponse(object content)
            : base((int)500, ImmutableDictionary<string, string>.Empty)
        {
            ContentData = content;
        }

        public InternalServerErrorResponse(object content, IReadOnlyDictionary<string, string> headers)
            : base((int)500, headers)
        {
            ContentData = content;
        }
    }
}
