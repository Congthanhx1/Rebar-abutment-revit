using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace Vetheprevit.MoCau
{
    public static class RebarSettingsStorage
    {
        // GUID duy nhất cho Schema (đảm bảo không đụng độ với add-in khác)
        private static readonly Guid schemaGuid = new Guid("9AE46369-2F11-420E-A8DF-59AC883B6AE2");

        public static Schema GetSchema()
        {
            Schema schema = Schema.Lookup(schemaGuid);
            if (schema == null)
            {
                SchemaBuilder builder = new SchemaBuilder(schemaGuid);
                builder.SetReadAccessLevel(AccessLevel.Public);
                builder.SetWriteAccessLevel(AccessLevel.Public);
                builder.SetSchemaName("MoCauRebarSettings");
                
                // Khởi tạo một trường kiểu Map để lưu nhiều cặp Key-Value
                builder.AddMapField("DataMap", typeof(string), typeof(string));
                
                schema = builder.Finish();
            }
            return schema;
        }

        public static void SaveSettings(Document doc, IDictionary<string, string> settings)
        {
            if (doc.IsFamilyDocument) return;

            Element projInfo = doc.ProjectInformation;
            if (projInfo == null) return;

            Schema schema = GetSchema();
            Entity entity = new Entity(schema);
            entity.Set("DataMap", settings);

            // Ghi vào document
            using (Transaction t = new Transaction(doc, "Lưu thông số rải thép mố cầu"))
            {
                try
                {
                    t.Start();
                    projInfo.SetEntity(entity);
                    t.Commit();
                }
                catch
                {
                    if (t.GetStatus() == TransactionStatus.Started)
                    {
                        t.RollBack();
                    }
                    throw;
                }
            }
        }

        public static IDictionary<string, string> LoadSettings(Document doc)
        {
            Element projInfo = doc.ProjectInformation;
            if (projInfo == null) return new Dictionary<string, string>();

            Schema schema = GetSchema();
            Entity entity = projInfo.GetEntity(schema);
            if (!entity.IsValid()) return new Dictionary<string, string>();

            return entity.Get<IDictionary<string, string>>("DataMap");
        }
    }
}
