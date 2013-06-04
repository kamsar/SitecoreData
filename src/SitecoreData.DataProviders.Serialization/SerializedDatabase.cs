using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Serialization;
using Sitecore.Data.Serialization.ObjectModel;

namespace SitecoreData.DataProviders.Serialization
{
    public class SerializedDatabase
    {
		private readonly List<SyncItem> _innerItems = new List<SyncItem>();

        public SerializedDatabase(string serializationPath)
        {
	        if (!Path.IsPathRooted(serializationPath))
		        serializationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, serializationPath);

            if (!Directory.Exists(serializationPath))
            {
                throw new Exception(string.Format("Path not found {0}, current Path {1}", Path.GetFullPath(serializationPath), AppDomain.CurrentDomain.BaseDirectory));
            }

            LoadTree(serializationPath);
        }

		public SyncItem GetItem(string idOrPath)
        {
            return GetItem(idOrPath, "en");
        }

	    public SyncItem GetItem(string idOrPath, string languageName)
	    {
		    var syncItem =
			    _innerItems.Find(
				    o =>
					o.ID.Equals(idOrPath, StringComparison.OrdinalIgnoreCase) ||
					o.ItemPath.Equals(idOrPath, StringComparison.OrdinalIgnoreCase));

		    if (syncItem == null)
		    {
			    return null;
		    }

			foreach (var version in syncItem.GetLatestVersions())
		    {
			    if (version.Language.Equals(languageName, StringComparison.OrdinalIgnoreCase))
			    {
				    return syncItem;
			    }
		    }

		    return null;
	    }

		public IEnumerable<SyncItem> GetItems(string pathOrId)
        {
            var items = new List<SyncItem>();
            var item = GetItem(pathOrId);

            if (item != null)
            {
                var children = GetChildren(item.ID).ToList();

                items.AddRange(children);

                foreach (var syncItem in children)
                {
					items.AddRange(GetItems(syncItem.ID));
                }
            }

            return items;
        }

		public IEnumerable<SyncItem> GetChildren(string pathOrId)
        {
            var items = new List<SyncItem>();
            var item = GetItem(pathOrId);

            if (item != null)
            {
				items = _innerItems.Where(o => o.ParentID.Equals(item.ID, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return items;
        }

		public IEnumerable<SyncItem> GetChildren(string pathOrId, string languageName)
        {
			var items = new List<SyncItem>();
            var item = GetItem(pathOrId, languageName);

            if (item != null)
            {
				items = _innerItems.Where(o => o.ParentID.Equals(item.ID, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return items;
        }

		public IEnumerable<SyncItem> GetItemsWithTemplate(Guid templateId)
		{
			string internalTemplateId = new ID(templateId).ToString();

			return _innerItems.Where(x => x.TemplateID.Equals(internalTemplateId, StringComparison.Ordinal)).ToArray();
		}

		public void CreateItem(SyncItem syncItem)
		{
			var newPath = PathUtils.GetFilePath(new ItemReference(syncItem.DatabaseName, syncItem.ItemPath).ToString());

			var parentPath = Path.GetDirectoryName(newPath);
			Directory.CreateDirectory(parentPath);

			using (var fileStream = File.Open(newPath, FileMode.CreateNew, FileAccess.Write, FileShare.Write))
			{
				using (var writer = new StreamWriter(fileStream))
				{
					syncItem.Serialize(writer);
				}
			}

			_innerItems.Add(syncItem);
		}

		private void LoadTree(string path)
		{
			LoadOneLevel(path);

			if (!Directory.Exists(path))
			{
				return;
			}

			var directories = Directory.GetDirectories(path);

			if (directories.Length > 1)
			{
				for (var i = 1; i < directories.Length; i++)
				{
					if (!"templates".Equals(Path.GetFileName(directories[i]), StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					var str = directories[0];

					directories[0] = directories[i];
					directories[i] = str;
				}
			}

			foreach (var directory in directories)
			{
				if (!CommonUtils.IsDirectoryHidden(directory))
				{
					LoadTree(directory);
				}
			}
		}

		private void LoadOneLevel(string path)
		{
			if (!Directory.Exists(path))
			{
				return;
			}

			foreach (var subPath in Directory.GetFiles(path, string.Format("*{0}", PathUtils.Extension)))
			{
				LoadItem(subPath);
			}
		}

		private void LoadItem(string path)
		{
			if (!File.Exists(path))
			{
				return;
			}

			using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (var reader = new StreamReader(fileStream))
				{
					var item = SyncItem.ReadItem(new Tokenizer(reader), false);

					if (!_innerItems.Exists(i => i.ID.Equals(item.ID)))
					{
						_innerItems.Add(item);
					}
				}
			}
		}
    }
}