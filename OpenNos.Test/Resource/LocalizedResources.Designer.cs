﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace OpenNos.Test.Resource {
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [DebuggerNonUserCode()]
    [CompilerGenerated()]
    internal class LocalizedResources {
        
        private static ResourceManager resourceMan;
        
        private static CultureInfo resourceCulture;
        
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal LocalizedResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        internal static ResourceManager ResourceManager {
            get {
                if (ReferenceEquals(resourceMan, null)) {
                    ResourceManager temp = new ResourceManager("OpenNos.Test.Resource.LocalizedResources", typeof(LocalizedResources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        internal static CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Account is already connected..
        /// </summary>
        internal static string ALREADY_CONNECTED {
            get {
                return ResourceManager.GetString("ALREADY_CONNECTED", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to You have been banned. Reason {0}, until {1}.
        /// </summary>
        internal static string BANNED {
            get {
                return ResourceManager.GetString("BANNED", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Connection closed by Client..
        /// </summary>
        internal static string CLIENT_CLOSED {
            get {
                return ResourceManager.GetString("CLIENT_CLOSED", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Connection closed..
        /// </summary>
        internal static string CLIENT_DISCONNECTED {
            get {
                return ResourceManager.GetString("CLIENT_DISCONNECTED", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Config loaded!.
        /// </summary>
        internal static string CONFIG_LOADED {
            get {
                return ResourceManager.GetString("CONFIG_LOADED", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CONNECT {0} Connected -- SessionId: {1}.
        /// </summary>
        internal static string CONNECTION {
            get {
                return ResourceManager.GetString("CONNECTION", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Connection closed..
        /// </summary>
        internal static string CONNECTION_CLOSED {
            get {
                return ResourceManager.GetString("CONNECTION_CLOSED", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Connection to the server has been lost..
        /// </summary>
        internal static string CONNECTION_LOST {
            get {
                return ResourceManager.GetString("CONNECTION_LOST", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Database has been initialized!.
        /// </summary>
        internal static string DATABASE_INITIALIZED {
            get {
                return ResourceManager.GetString("DATABASE_INITIALIZED", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Database may not be up to date. Please consider updating your database..
        /// </summary>
        internal static string DATABASE_NOT_UPTODATE {
            get {
                return ResourceManager.GetString("DATABASE_NOT_UPTODATE", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Client disconnected! ClientId = .
        /// </summary>
        internal static string DISCONNECT {
            get {
                return ResourceManager.GetString("DISCONNECT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Forced Disconnecting of client {0}, too much connections..
        /// </summary>
        internal static string FORCED_DISCONNECT {
            get {
                return ResourceManager.GetString("FORCED_DISCONNECT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Wrong Id or Password!.
        /// </summary>
        internal static string IDERROR {
            get {
                return ResourceManager.GetString("IDERROR", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Server is currently under maintenance. Maintenance estimated time {0}.
        /// </summary>
        internal static string MAINTENANCE {
            get {
                return ResourceManager.GetString("MAINTENANCE", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message received {0} on client {1}.
        /// </summary>
        internal static string MESSAGE_RECEIVED {
            get {
                return ResourceManager.GetString("MESSAGE_RECEIVED", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message sent to client .
        /// </summary>
        internal static string MSG_SENT {
            get {
                return ResourceManager.GetString("MSG_SENT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to New client connected. ClientId = .
        /// </summary>
        internal static string NEW_CONNECT {
            get {
                return ResourceManager.GetString("NEW_CONNECT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Account not validate!.
        /// </summary>
        internal static string NOTVALIDATE {
            get {
                return ResourceManager.GetString("NOTVALIDATE", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to send packet {0} to client {1}, {2}..
        /// </summary>
        internal static string PACKET_FAILURE {
            get {
                return ResourceManager.GetString("PACKET_FAILURE", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to LoginServer started successfully..
        /// </summary>
        internal static string STARTED {
            get {
                return ResourceManager.GetString("STARTED", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message is too big ({0} bytes). Max allowed length is {1} bytes..
        /// </summary>
        internal static string TOO_BIG {
            get {
                return ResourceManager.GetString("TOO_BIG", resourceCulture);
            }
        }
    }
}
