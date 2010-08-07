// Guids.cs
// MUST match guids.h
using System;

namespace Physique.VS2010Addin
{
    static class GuidList
    {
        public const string guidVS2010AddinPkgString = "fec7f6b0-3827-41c3-8bb9-7d9967ea1444";
        public const string guidVS2010AddinCmdSetString = "a009d743-0097-4fbf-9ef1-53476b40561e";

        public static readonly Guid guidVS2010AddinCmdSet = new Guid(guidVS2010AddinCmdSetString);
    };
}