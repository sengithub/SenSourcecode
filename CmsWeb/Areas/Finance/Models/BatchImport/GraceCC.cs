/* Author: David Carroll
 * Copyright (c) 2008, 2009 Bellevue Baptist Church 
 * Licensed under the GNU General Public License (GPL v2)
 * you may not use this code except in compliance with the License.
 * You may obtain a copy of the License at http://bvcms.codeplex.com/license 
 */
using System;
using System.Linq;
using CmsData;
using LumenWorks.Framework.IO.Csv;
using UtilityExtensions;

namespace CmsWeb.Models
{
    public partial class BatchImportContributions
    {
        public static int? BatchProcessGraceCC(CsvReader csv, DateTime date, int? fundid)
        {
            var fundList = (from f in DbUtil.Db.ContributionFunds
                            orderby f.FundId
                            select f.FundId).ToList();

            var cols = csv.GetFieldHeaders();
            BundleHeader bh = null;
            var fid = fundid ?? FirstFundId();

            while (csv.ReadNextRecord())
            {
                var dt = csv[3].ToDate();
                var amount = csv[13];
                if (!amount.HasValue() || !dt.HasValue)
                    continue;

                var routing = csv[10];
                var account = csv[9];
                var checkno = csv[11];

                if (bh == null)
                    bh = GetBundleHeader(dt.Value, DateTime.Now);

                var bd = AddContributionDetail(date, fid, amount, checkno, routing, account);

                bh.BundleDetails.Add(bd);
            }

            FinishBundle(bh);

            return bh.BundleHeaderId;
        }
    }
}