using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tenis3t.Helpers
{
    // Helpers/PermissionConstants.cs
    public static class PermissionConstants
    {
        public static readonly string[] AdminUsers = { "3T", "MIMAU" };

        public static bool IsAdminUser(string userName)
        {
            return AdminUsers.Contains(userName);
        }
    }
}