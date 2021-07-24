﻿using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Cathei.BakingSheet.Raw
{
    public interface IRawSheetImporterPage
    {
        string GetCell(int col, int row);
    }

    public static class RawSheetImporterPageExtensions
    {
        public static bool IsEmptyRow(this IRawSheetImporterPage page, int pageRow)
        {
            int pageColumn = 0;

            while (!string.IsNullOrEmpty(page.GetCell(pageColumn, 0)))
            {
                if (!string.IsNullOrEmpty(page.GetCell(pageColumn, pageRow)))
                    return false;
                
                pageColumn += 1;
            }

            return true;
        }

        public static void Import(this IRawSheetImporterPage page, RawSheetImporter importer, SheetConvertingContext context, Sheet sheet)
        {
            ISheetRow sheetRow = null;

            var parentTag = context.Tag;

            for (int pageRow = 1; !page.IsEmptyRow(pageRow); ++pageRow)
            {
                var rowId = page.GetCell(0, pageRow);

                if (!string.IsNullOrEmpty(rowId))
                {
                    context.SetTag(parentTag, rowId);

                    sheetRow = Activator.CreateInstance(sheet.RowType) as ISheetRow;

                    page.ImportToObject(importer, context, sheet, pageRow);

                    (sheet as IDictionary).Add(sheetRow.Id, sheetRow);
                }

                if (sheetRow is ISheetRowArray sheetRowArray)
                {
                    context.SetTag(parentTag, sheetRow.Id, sheetRowArray.Arr.Count);

                    var sheetElem = Activator.CreateInstance(sheetRowArray.ElemType);

                    page.ImportToObject(importer, context, sheetElem, pageRow);

                    sheetRowArray.Arr.Add(sheetElem);
                }
            }
        }

        private static void ImportToObject(this IRawSheetImporterPage page, RawSheetImporter importer, SheetConvertingContext context, object obj, int pageRow)
        {
            var type = obj.GetType();
            var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty;

            var parentTag = context.Tag;
            var pageColumn = 0;

            while (!string.IsNullOrEmpty(page.GetCell(pageColumn, 0)))
            {
                var columnValue = page.GetCell(pageColumn, 0);
                var cellValue = page.GetCell(pageColumn, pageRow);

                var prop = type.GetProperty(columnValue, bindingFlags);
                if (prop == null)
                    continue;

                context.SetTag(parentTag, columnValue);

                try
                {
                    object value = importer.StringToValue(context, prop.PropertyType, cellValue);
                    prop.SetValue(obj, value);
                }
                catch (Exception ex)
                {
                    context.Logger.LogError(ex, $"[{context.Tag}] Failed to convert value \"{cellValue}\" of type {prop.PropertyType}");
                }
            }
        }
    }
}
