using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace Vetheprevit.TienIch
{
    public class RebarMarkerData
    {
        public string Creator { get; set; }
        public int Version { get; set; }
        public int HostId { get; set; }
        public string GroupCode { get; set; }
    }

    public static class RebarMarkerService
    {
        private static readonly Guid schemaGuid = new Guid("A1B2C3D4-5E6F-4789-9ABC-DEF012345678");

        public static Schema GetSchema()
        {
            Schema schema = Schema.Lookup(schemaGuid);
            if (schema == null)
            {
                SchemaBuilder builder = new SchemaBuilder(schemaGuid);
                builder.SetReadAccessLevel(AccessLevel.Public);
                builder.SetWriteAccessLevel(AccessLevel.Public);
                builder.SetSchemaName("RebarAbutmentMarker");

                builder.AddSimpleField("Creator", typeof(string));
                builder.AddSimpleField("Version", typeof(int));
                builder.AddSimpleField("HostId", typeof(int));
                builder.AddSimpleField("GroupCode", typeof(string));

                schema = builder.Finish();
            }
            return schema;
        }

        public static void SetMarker(Rebar rebar, ElementId hostId, string groupCode)
        {
            if (rebar == null) return;
            Document doc = rebar.Document;
            int hostIdValue = GetStorageHostId(hostId);

            Schema schema = GetSchema();
            Entity entity = new Entity(schema);
            entity.Set("Creator", "RebarAbutmentTool");
            entity.Set("Version", 1);
            entity.Set("HostId", hostIdValue);
            entity.Set("GroupCode", groupCode);

            // Rebar creation/modification happens inside an active transaction in the tool
            rebar.SetEntity(entity);
        }

        public static RebarMarkerData GetMarker(Rebar rebar)
        {
            if (rebar == null) return null;
            Schema schema = GetSchema();
            Entity entity = rebar.GetEntity(schema);
            
            if (!entity.IsValid()) return null;

            try
            {
                return new RebarMarkerData
                {
                    Creator = entity.Get<string>("Creator"),
                    Version = entity.Get<int>("Version"),
                    HostId = entity.Get<int>("HostId"),
                    GroupCode = entity.Get<string>("GroupCode")
                };
            }
            catch
            {
                return null;
            }
        }

        private static int GetStorageHostId(ElementId hostId)
        {
            if (hostId == null || hostId == ElementId.InvalidElementId)
                return -1;

            long value = hostId.Value;
            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Host ElementId is outside the supported Extensible Storage range.");
            }

            return (int)value;
        }
    }
}
