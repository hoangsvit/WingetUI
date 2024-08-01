using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.Interfaces.ManagerProviders;

namespace UniGetUI.PackageEngine.Classes.Manager.Providers
{
    public abstract class BaseSourceProvider<ManagerT> : ISourceProvider where ManagerT : IPackageManager
    {
        public ISourceFactory SourceFactory { get; }
        protected ManagerT Manager;

        public BaseSourceProvider(ManagerT manager)
        {
            Manager = manager;
            SourceFactory = new SourceFactory(manager);
        }

        public abstract string[] GetAddSourceParameters(IManagerSource source);
        public abstract string[] GetRemoveSourceParameters(IManagerSource source);
        public abstract OperationVeredict GetAddSourceOperationVeredict(IManagerSource source, int ReturnCode, string[] Output);
        public abstract OperationVeredict GetRemoveSourceOperationVeredict(IManagerSource source, int ReturnCode, string[] Output);

        /// <summary>
        /// Loads the sources for the manager. This method SHOULD NOT handle exceptions
        /// </summary>
        protected abstract Task<IManagerSource[]> GetSources_UnSafe();
        public virtual async Task<IManagerSource[]> GetSources()
        {
            IManagerSource[] sources = await GetSources_UnSafe();
            SourceFactory.Reset();

            foreach (IManagerSource source in sources)
            {
                SourceFactory.AddSource(source);
            }

            return sources;
        }
    }
}
