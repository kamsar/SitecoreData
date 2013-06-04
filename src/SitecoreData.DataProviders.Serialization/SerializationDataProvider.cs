using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Serialization;
using Sitecore.Data.Serialization.ObjectModel;

namespace SitecoreData.DataProviders.Serialization
{
	public class SerializationDataProvider : DataProviderBase, IWritableDataProvider
	{
		private readonly SerializedDatabase _database;

		public SerializationDataProvider(string connectionString)
			: base(connectionString)
		{
			_database = new SerializedDatabase(connectionString);
		}

		public override ItemDto GetItem(Guid id)
		{
			var syncItem = _database.GetItem(new ID(id).ToString());

			if (syncItem == null)
			{
				return null;
			}

			return new ItemDto
					   {
						   Id = Guid.Parse(syncItem.ID),
						   BranchId = Guid.Parse(syncItem.BranchId),
						   Name = syncItem.Name,
						   ParentId = Guid.Parse(syncItem.ParentID),
						   TemplateId = Guid.Parse(syncItem.TemplateID),
						   FieldValues = LoadFieldValues(syncItem)
					   };
		}

		private List<FieldDto> LoadFieldValues(SyncItem syncItem)
		{
			var fields = new List<FieldDto>();

			foreach (var field in syncItem.SharedFields)
			{
				var fieldDto = new FieldDto {Id = Guid.Parse(field.FieldID), Value = field.FieldValue};
				fields.Add(fieldDto);
			}

			foreach (var version in syncItem.Versions)
			{
				foreach (var field in version.Fields)
				{
					var fieldDto = new FieldDto {Id = Guid.Parse(field.FieldID), Value = field.FieldValue, Language = version.Language, Version = int.Parse(version.Version)};
					fields.Add(fieldDto);
				}
			}

			return fields;
		}

		public override Guid GetParentId(Guid id)
		{
			var result = GetItem(id);

			return result != null ? (result.ParentId != Guid.Empty ? result.ParentId : ID.Null.ToGuid()) : Guid.Empty;
		}

		public override IEnumerable<Guid> GetChildIds(Guid parentId)
		{
			return _database.GetChildren(new ID(parentId).ToString()).Select(x => Guid.Parse(x.ID)).ToArray();
		}

		public override IEnumerable<Guid> GetTemplateIds(Guid templateId)
		{
			return _database.GetItemsWithTemplate(templateId).Select(x => Guid.Parse(x.ID)).ToArray();
		}

		public override IEnumerable<ItemDto> GetItemsInWorkflowState(Guid workflowStateId)
		{
			return new ItemDto[] { };
		}

		public bool CreateItem(Guid id, string name, Guid templateId, Guid parentId)
		{
			var parent = _database.GetItem(new ID(parentId).ToString());
			var template = _database.GetItem(new ID(templateId).ToString());

			string newItemFullPath = parent.ItemPath + "/" + name;

			var syncItem = new SyncItem();
			syncItem.ID = new ID(id).ToString();
			syncItem.Name = name;
			syncItem.TemplateID = new ID(templateId).ToString();
			syncItem.TemplateName = template.Name;
			syncItem.ParentID = new ID(parentId).ToString();
			syncItem.DatabaseName = "master"; // ugly
			syncItem.ItemPath = newItemFullPath;
			syncItem.MasterID = ID.Null.ToString();
			
			_database.CreateItem(syncItem);

			return true;
		}

		public bool DeleteItem(Guid id)
		{
			throw new NotImplementedException();
		}

		public void Store(ItemDto itemDto)
		{
			throw new NotImplementedException();
		}
	}
}