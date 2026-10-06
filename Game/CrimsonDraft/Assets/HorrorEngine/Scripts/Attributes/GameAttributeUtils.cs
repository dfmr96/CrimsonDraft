using System;

namespace HorrorEngine
{
    public enum GameAttributeOp
    {
        Add,
        Subtract,
        Override,
        Scale
    }

    public class GameAttributeUtils
    {
        public static GameAttributes GetAttributes(GameAttributeDomain domain)
        {
            return GameManager.Instance.GetAttributes(domain);
        }

        // --------------------------------------------------------------------

        public static bool IsDefined(GameAttribute attribute)
        {
            return GetAttributes(attribute.Domain).IsDefined(attribute);
        }

        // --------------------------------------------------------------------

        public static void Set(GameAttribute attribute, string val)
        {
            GetAttributes(attribute.Domain).Set(attribute, val);
        }

        // --------------------------------------------------------------------

        public static string Get(GameAttribute attribute, string defaultVal = "")
        {
            return GetAttributes(attribute.Domain).Get(attribute);
        }

        // --------------------------------------------------------------------

        public static void SetAsInt(GameAttribute gameAttribute, int val)
        {
            Set(gameAttribute, val.ToString());
        }

        // --------------------------------------------------------------------

        public static int GetAsInt(GameAttribute gameAttribute, int defaultVal = 0)
        {
            if (IsDefined(gameAttribute))
                return Convert.ToInt32(Get(gameAttribute));
            else
                return defaultVal;
        }

        // --------------------------------------------------------------------

        public static void SetAsFloat(GameAttribute gameAttribute, float val)
        {
            Set(gameAttribute, val.ToString());
        }

        // --------------------------------------------------------------------

        public static float GetAsFloat(GameAttribute gameAttribute, float defaultVal = 0)
        {
            if (IsDefined(gameAttribute))
                return (float)Convert.ToDouble(Get(gameAttribute));
            else
                return defaultVal;
        }

        // --------------------------------------------------------------------

        public static void PerformOperation(GameAttribute attribute, float amount, GameAttributeOp op)
        {
            float currentVal = GameAttributeUtils.GetAsFloat(attribute);
            switch (op)
            {
                case GameAttributeOp.Add:
                    GameAttributeUtils.SetAsFloat(attribute, currentVal + amount);
                    break;
                case GameAttributeOp.Subtract:
                    GameAttributeUtils.SetAsFloat(attribute, currentVal - amount);
                    break;
                case GameAttributeOp.Override:
                    GameAttributeUtils.Set(attribute, amount.ToString());
                    break;
                case GameAttributeOp.Scale:
                    GameAttributeUtils.SetAsFloat(attribute, currentVal * amount);
                    break;
            }
        }
    }

}