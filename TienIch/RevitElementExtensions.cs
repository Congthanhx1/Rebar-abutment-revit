using Autodesk.Revit.DB;

namespace Nice3point.Revit.Extensions
{
    internal static class ElementIdExtensions
    {
        public static Element ToElement(this ElementId elementId, Document document)
        {
            if (elementId == null || document == null) return null;

            return document.GetElement(elementId);
        }

        public static T ToElement<T>(this ElementId elementId, Document document)
            where T : Element
        {
            return elementId.ToElement(document) as T;
        }
    }

    internal static class ElementExtensions
    {
        public static Parameter FindParameter(
            this Element element,
            BuiltInParameter builtInParameter)
        {
            if (element == null) return null;

            return element.get_Parameter(builtInParameter);
        }

        public static Parameter FindParameter(this Element element, string parameterName)
        {
            if (element == null || string.IsNullOrWhiteSpace(parameterName))
                return null;

            Parameter parameter = element.LookupParameter(parameterName);
            if (parameter != null) return parameter;

            foreach (Parameter candidate in element.Parameters)
            {
                if (candidate?.Definition == null) continue;
                if (candidate.Definition.Name == parameterName) return candidate;
            }

            return null;
        }
    }
}
