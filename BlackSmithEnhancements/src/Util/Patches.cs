﻿using System;
using System.Linq;
using BlackSmithEnhancements.Behavior.Item;
using BlackSmithEnhancements.Item;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BlackSmithEnhancements.Util
{
    [HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.DropSlotIfHot))]
    public class PlayerDropSlotIfHotPatch
    {
         // Blacksmith Gloves by Arahvin.  Fixed by me //
        //[https://mods.vintagestory.at/show/mod/6581]//

        [HarmonyPrefix]
        public static bool Gear_Has_Heat_Resistant(ItemSlot slot, IPlayer player)
        {
            if (slot.Empty || player == null || player.WorldData.CurrentGameMode == EnumGameMode.Creative) return false;
            if (player.Entity?
                    .GetBehavior<EntityBehaviorPlayerInventory>() is not { Inventory: not null } playerInventory) 
                return true;
            foreach (var itemSlot in playerInventory.Inventory)
            {
                if (itemSlot.BackgroundIcon != "gloves")
                {
                    continue;
                }

                if (itemSlot.Empty)
                {
                    return true;
                }

                var itemstack = itemSlot.Itemstack;
                bool? isHeatResistant;
                if (itemstack == null)
                {
                    isHeatResistant = null;
                }
                else
                {
                    var attributes = itemstack.Collectible.Attributes;
                    isHeatResistant = attributes?.IsTrue("heatResistant");
                }
                return isHeatResistant != null && !isHeatResistant.Value;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(BlockBarrel), "OnBlockInteractStart")]
    public class OnBlockInteractStartPatch
    {

        [HarmonyPrefix]
        public static bool BlockBarrel_OnBlockInteractStart_Patch(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && world != null)
            {
                if (byPlayer.Entity.Controls.ShiftKey == false && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
                {
                    var heldItem = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Item;

                    if (heldItem != null)
                    {
                        if (heldItem.HasBehavior<ItemBehaviorQuenching>())
                        {
                            return true;
                        }
                    }
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(BlockEntityForge), "OnPlayerInteract")]
    public class OnPlayerInteractPatch
    {

        [HarmonyPrefix]
        public static bool BlockEntityForge_OnPlayerInteract_Patch(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null || world == null) return true;
            if (byPlayer.InventoryManager.ActiveHotbarSlot.Empty) return true;
            var heldItem = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Item;
            return heldItem is not ItemBellow;
        }
    }

    [HarmonyPatch(typeof(Block), "OnEntityInside")]
    public class OnEntityInsidePatch
    {

        [HarmonyPrefix]
        public static bool BlockForge_OnEntityInside_Patch(IWorldAccessor world, Entity entity, BlockPos pos)
        {
            if (world == null) return false;
            if (entity is not EntityPlayer entityPlayer) return false;
            if (pos == null) return false;
            if (!(world.Rand.NextDouble() < 0.05) ||
                world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityForge blockEntityForge)
                return true;
            if (entityPlayer.Player.WorldData.CurrentGameMode != EnumGameMode.Survival) return false;
            if (blockEntityForge.IsBurning && entityPlayer.Pos.AsBlockPos.UpCopy() == blockEntityForge.Pos.UpCopy())
            {
                entity.ReceiveDamage(new DamageSource
                {
                    Source = EnumDamageSource.Block,
                    SourceBlock = blockEntityForge.Block,
                    Type = EnumDamageType.Fire,
                    SourcePos = pos.ToVec3d()
                }, 0.5f);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(BlockEntityFirepit), "OnPlayerRightClick")]
    public class OnPlayerRightClickPatch
    {

        [HarmonyPrefix]
        public static bool BlockEntityFirepit(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return true;
            if (byPlayer.InventoryManager.ActiveHotbarSlot.Empty) return true;
            var heldItem = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Item;
            return heldItem is not ItemBellow;
        }
    }

    [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnCreatedByCrafting))]
    public static class OnCreatedByCraftingPatch
    {
        // Stop crafting of items with a temperature above 300 (to encourage quenching)
        [HarmonyPrefix]
        public static bool OnCreatedByCrafting_Postfix(
            ItemSlot[] allInputslots,
            ItemSlot outputSlot,
            GridRecipe byRecipe)
        {
            if (outputSlot.Empty) return true;
            if (outputSlot.Inventory?.Api?.World is not { } world) return true;
            var hotSlots = allInputslots.Where(slot => slot.Itemstack?.GetTemperature(world) > 300.0).ToArray();
            if (hotSlots.Length == 0) return true;
            if (world.Side == EnumAppSide.Server && 
                outputSlot.Inventory is InventoryBasePlayer inventory &&
                inventory.Player is IServerPlayer serverPlayer
               )
            {
                var sapi = (ICoreServerAPI) world.Api;
                sapi.SendIngameError(serverPlayer,
                    "Must quench first",
                    Lang.Get("ingameerror-mustquenchfirst", hotSlots[0]?.Itemstack?.GetHeldItemName().ToLower()));
            }
            outputSlot.Itemstack = null;
            return false;
        }
        
        private static string GetHeldItemName(this ItemStack stack)
        {
            return stack.Collectible.GetHeldItemName(stack);
        }

        private static float GetTemperature(this ItemStack stack, IWorldAccessor world)
        {
            var temperature = stack.Collectible.GetTemperature(world, stack);
            Console.Error.WriteLine($"temperature: {temperature}");
            return temperature;
        }
    }

}
