using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;


namespace FixSlowFile
{
    public class MyParameterValue
    {
        public MyProjectSharedParameter projectParam;
        public StorageType storageType;
        public bool IsValid = false;

        public string StringValue;
        public double DoubleValue;
        public int IntegerValue;
        public int ElementIdValue;

        public MyParameterValue(Parameter revitParam)
        {
            storageType = revitParam.StorageType;
            switch (storageType)
            {
                case StorageType.None:
                    break;
                case StorageType.Integer:
                    IntegerValue = revitParam.AsInteger();
                    IsValid = true;
                    break;
                case StorageType.Double:
                    DoubleValue = revitParam.AsDouble();
                    IsValid = true;
                    break;
                case StorageType.String:
                    StringValue = revitParam.AsString();
                    IsValid = true;
                    break;
                case StorageType.ElementId:
                    ElementIdValue = revitParam.AsElementId().IntegerValue;
                    IsValid = true;
                    break;
                default:
                    IsValid = false;
                    break;
            }
        }

        public void SetValue(Parameter revitParam)
        {
            if (revitParam.IsReadOnly) return;
            switch (revitParam.StorageType)
            {
                case StorageType.None:
                    return;
                case StorageType.Integer:
                    revitParam.Set(IntegerValue);
                    return;

                case StorageType.Double:
                    revitParam.Set(DoubleValue);
                    return;

                case StorageType.String:
                    revitParam.Set(StringValue);
                    return;

                case StorageType.ElementId:
                    ElementId id = new ElementId(ElementIdValue);
                    revitParam.Set(id);
                    return;

                default:
                    throw new Exception("Invalid value for StorageType");
            }
        }
    }
}
