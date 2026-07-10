using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Autodesk.Revit.UI;

namespace Vetheprevit.MoCau
{
    public class App : IExternalApplication
    {
        private static bool _assemblyResolverRegistered;
        private static AssemblyDependencyResolver _dependencyResolver;
        private static AssemblyLoadContext _addinLoadContext;

        public Result OnStartup(UIControlledApplication application)
        {
            RegisterAssemblyResolver();

            // Tên Tab do bạn yêu cầu
            string tabName = "TOOL CT";
            
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                // Tab có thể đã tồn tại, bỏ qua lỗi
            }

            // Tạo một Panel bên trong Tab
            string panelName = "Mố Cầu";
            RibbonPanel ribbonPanel = null;
            
            // Kiểm tra xem Panel đã tồn tại chưa
            var panels = application.GetRibbonPanels(tabName);
            foreach (var panel in panels)
            {
                if (panel.Name == panelName)
                {
                    ribbonPanel = panel;
                    break;
                }
            }
            
            // Nếu chưa có thì tạo mới
            if (ribbonPanel == null)
            {
                ribbonPanel = application.CreateRibbonPanel(tabName, panelName);
            }

            // Đường dẫn đến file DLL của Add-in đang chạy
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            // Tạo thông tin cho Nút (Button)
            PushButtonData buttonData = new PushButtonData(
                "cmdDrawMoCauRebar", 
                "Vẽ Thép\nBệ Mố", 
                thisAssemblyPath, 
                "Vetheprevit.MoCau.CmdDrawMoCauRebar");
            
            // Thêm chú thích khi di chuột vào nút
            buttonData.ToolTip = "Tự động vẽ thép lớp dưới cho bệ mố cầu hình chữ U.";

            // Bạn có thể thêm Icon ở đây nếu có (LargeImage)
            // ví dụ: buttonData.LargeImage = new BitmapImage(new Uri("pack://application:,,,/Vetheprevit;component/Resources/icon32.png"));

            // Gắn nút vào Panel
            ribbonPanel.AddItem(buttonData);

            PushButtonData tiltButtonData = new PushButtonData(
                "cmdTiltRebarEnd", 
                "Né Thép\nGiao Nhau", 
                thisAssemblyPath, 
                "Vetheprevit.MoCau.CmdTiltRebarEnd");
            
            tiltButtonData.ToolTip = "Bẻ cong một đầu của thanh thép để tránh đụng với thanh thép khác.";
            ribbonPanel.AddItem(tiltButtonData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        internal static void RegisterAssemblyResolver()
        {
            if (_assemblyResolverRegistered) return;
            _assemblyResolverRegistered = true;
            _addinLoadContext =
                AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()) ??
                AssemblyLoadContext.Default;
            _dependencyResolver = new AssemblyDependencyResolver(
                Assembly.GetExecutingAssembly().Location);

            _addinLoadContext.Resolving += ResolveMissingAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                ResolveMissingAssembly(
                    _addinLoadContext ?? AssemblyLoadContext.Default,
                    new AssemblyName(args.Name));
        }

        private static Assembly ResolveMissingAssembly(
            AssemblyLoadContext context,
            AssemblyName assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName?.Name))
                return null;

            string dependencyPath = _dependencyResolver?.ResolveAssemblyToPath(assemblyName);
            Assembly dependency = LoadAssemblyFromPath(context, dependencyPath);
            if (dependency != null) return dependency;

            foreach (string folder in GetAssemblyProbeFolders())
            {
                string assemblyPath = Path.Combine(folder, assemblyName.Name + ".dll");
                dependency = LoadAssemblyFromPath(context, assemblyPath);
                if (dependency != null) return dependency;
            }

            return null;
        }

        private static Assembly LoadAssemblyFromPath(
            AssemblyLoadContext context,
            string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath)) return null;
            if (!File.Exists(assemblyPath)) return null;

            AssemblyName requestedName;
            try
            {
                requestedName = AssemblyName.GetAssemblyName(assemblyPath);
            }
            catch
            {
                return null;
            }

            foreach (Assembly loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                AssemblyName loadedName = loadedAssembly.GetName();
                if (string.Equals(
                        loadedName.Name,
                        requestedName.Name,
                        StringComparison.OrdinalIgnoreCase))
                    return loadedAssembly;
            }

            try
            {
                return context.LoadFromAssemblyPath(assemblyPath);
            }
            catch
            {
                try
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static IEnumerable<string> GetAssemblyProbeFolders()
        {
            HashSet<string> folders =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddFolder(folders, Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location));
            AddFolder(folders, AppContext.BaseDirectory);
            AddFolder(folders, Environment.CurrentDirectory);

            return folders;
        }

        private static void AddFolder(ISet<string> folders, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return;
            if (!Directory.Exists(folder)) return;

            folders.Add(folder);
        }
    }
}
