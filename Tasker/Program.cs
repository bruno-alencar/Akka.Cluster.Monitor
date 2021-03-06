﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Http;
using Akka.Actor;
using Akka.Cluster;
using Ninject.Modules;
using Shared;
using Shared.Actors;
using Topshelf;
using Topshelf.Ninject;

namespace Tasker
{
    class Program
    {
        public static ActorSystem ClusterSystem { get; set; }
        public static IActorRef JobMaster { get; set; }

        public static IActorRef ClusterHelper;
        public static IActorRef ClusterStatus;

        #region Trap application termination
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool Handler(CtrlType sig)
        {
            Console.WriteLine("Exiting system due to external CTRL-C, or process kill, or shutdown");

            //do your cleanup here
            ClusterHelper.Tell(new ClusterHelper.RemoveMember());
            Thread.Sleep(5000); // Give the Remove time to actually remove...

            ClusterSystem.Terminate();
            Console.WriteLine("Cleanup complete");

            //shutdown right away so there are no lingering threads
            Environment.Exit(-1);

            return true;
        }
        #endregion

        static int Main(string[] args)
        {
            // Some biolerplate to react to close window event, CTRL-C, kill, etc
            if (Environment.UserInteractive)
            {
                _handler += new EventHandler(Handler);
                SetConsoleCtrlHandler(_handler, true);
            }
            
            var topShelfExitCode = (int)HostFactory.Run(hostConfiguratior =>
            {
                hostConfiguratior.UseAssemblyInfoForServiceInfo();
                hostConfiguratior.SetServiceName("Tasker");
                hostConfiguratior.SetDisplayName("Tasker");
                hostConfiguratior.SetDescription("Tasker");
                hostConfiguratior.DependsOnEventLog();
                hostConfiguratior.UseNinject(new ServiceModule());
                hostConfiguratior.RunAsLocalSystem();
                //hostConfiguratior.StartAutomatically();
                hostConfiguratior.Service<TaskerService>((serviceController) =>
                {
                    serviceController.ConstructUsingNinject();
                    serviceController.WhenStarted((service, hostControl) => service.Start(hostControl));
                    serviceController.WhenStopped((service, hostControl) => service.Stop(hostControl));
                    //serviceController.WebApiEndpoint(api =>
                    //    api.OnHost("http", "localhost", 8080)
                    //        .ConfigureRoutes(Configure)
                    //        .Build());
                });
                hostConfiguratior.EnableServiceRecovery(r =>
                {
                    r.RestartService(1);
                });

            });
            return topShelfExitCode;
        }
        private static void Configure(HttpRouteCollection routes)
        {
            routes.MapHttpRoute(
                    "DefaultApiWithId",
                    "Api/{controller}/{id}",
                    new { id = RouteParameter.Optional },
                    new { id = @"\d+" });
        }
    }

    internal class ServiceModule : NinjectModule
    {
        public override void Load()
        {
        }
    }

}

