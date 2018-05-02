using System;
using System.Collections.Generic;

namespace Restup.WebServer.Attributes
{
	public class AuthorizeAttribute : Attribute
	{
        public List<string> Roles = new List<string>();
             
	    public AuthorizeAttribute()
	    {
	        
	    }

	    public AuthorizeAttribute(params string[] roles)
	    {
	        Roles.AddRange(roles);
	    }
	}
}
