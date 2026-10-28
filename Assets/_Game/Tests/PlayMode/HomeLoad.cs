using UtsavNights.Core.Home;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Fills a design with decorations up to a tier's budget (every item in turn), for performance tests.</summary>
    public static class HomeLoad
    {
        public static int FillToBudget(HouseDesign design, int tier)
        {
            int units = DecorPlacement.UnitsUsed(design);
            int i = 0;
            bool added = true;
            while (added)
            {
                added = false;
                foreach (DecorInfo info in HomeCatalog.Decorations)
                {
                    int cost = DecorBudget.CostOf(info, DecorBudget.PathUnitLength);
                    if (units + cost > DecorBudget.Units[tier])
                    {
                        continue;
                    }

                    float x = -3f + i % 12 * 0.5f, z = 1f + i / 12 % 8 * 0.5f;
                    var d = new PlacedDecor { uid = design.nextUid++, item = info.Id, x = x, z = z, variant = i % info.Variants, scale = 1f };
                    if (info.Mode == PlacementMode.Path)
                    {
                        d.x = -1.5f;
                        d.y = 2f + i % 6 * 0.6f;
                        d.ex = 1.5f;
                        d.ey = d.y;
                        d.sag = 0.2f;
                    }

                    design.decorations.Add(d);
                    units += cost;
                    i++;
                    added = true;
                }
            }

            return units;
        }
    }
}
