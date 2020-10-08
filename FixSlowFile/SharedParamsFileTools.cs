﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace FixSlowFile
{
    public static class SharedParamsFileTools
    {
        public static bool CheckParameterExistsInFile(DefinitionFile deffile, Guid paramGuid)
        {
            if(deffile == null)
            {
                throw new Exception("Не подключен файл общих параметров");
            }
            foreach(DefinitionGroup defgr in deffile.Groups)
            {
                foreach(ExternalDefinition exdf in defgr.Definitions)
                {
                    if(paramGuid.Equals(exdf.GUID))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static ExternalDefinition AddParameterToDefFile(DefinitionFile defFile, string groupName, MyProjectSharedParameter myparam)
        {
            DefinitionGroup tempGroup = null;
           List <DefinitionGroup> groups = defFile.Groups.Where(i => i.Name == groupName).ToList();
            if(groups.Count == 0)
            {
                try
                {
                    tempGroup = defFile.Groups.Create(groupName);
                }
                catch (Exception ex)
                {
                    throw new Exception("Не удалось создать группу " + groupName + " в файле общих параметров " + defFile.Filename);
                }
            }
            else
            {
                tempGroup = groups.First();
            }
            
            
            Definitions defs = tempGroup.Definitions;
            ExternalDefinitionCreationOptions defOptions =
                  new ExternalDefinitionCreationOptions(myparam.Name, myparam.def.ParameterType);
            defOptions.GUID = myparam.guid;

            ExternalDefinition exDef = defs.Create(defOptions) as ExternalDefinition;
            if(exDef == null)
            {
                throw new Exception("Не удалось создать общий параметр " + myparam.Name);
            }
            return exDef;
        }
    }
}
