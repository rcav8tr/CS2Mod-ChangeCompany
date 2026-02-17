using Colossal.Localization;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Input;
using Game.SceneFlow;
using Game.UI;
using System;
using UnityEngine.InputSystem;

namespace ChangeCompany
{
    /// <summary>
    /// The system for the production balance UI.
    /// </summary>
    public partial class ProductionBalanceUISystem : UISystemBase
    {
        // Other systems.
        private ProductionBalanceSystem _productionBalanceSystem;

        // Timing control.
        private long _previousRealTimeTicks;
        
        // Localization.
        private LocalizationManager _localizationManager;

        // Production balance info.
        private ProductionBalanceInfo _productionBalanceInfoIndustrial;
        private ProductionBalanceInfo _productionBalanceInfoOffice;

        // Binding names.
        private const string BindingNameProductionBalanceButtonClicked  = "ProductionBalanceButtonClicked";
        private const string BindingNameProductionBalancePanelMoved     = "ProductionBalancePanelMoved";

        private const string BindingNameProductionBalanceUISettings     = "ProductionBalanceUISettings";
        private const string BindingNameProductionBalanceInfoIndustrial = "ProductionBalanceInfoIndustrial";
        private const string BindingNameProductionBalanceInfoOffice     = "ProductionBalanceInfoOffice";

        // C# to UI bindings.
        private RawValueBinding _bindingProductionBalanceUISettings;
        private RawValueBinding _bindingProductionBalanceInfoIndustrial;
        private RawValueBinding _bindingProductionBalanceInfoOffice;

        
        /// <summary>
        /// System is valid only in game, not editor.
        /// </summary>
        public override GameMode gameMode
        {
            get { return GameMode.Game; }
        }

        /// <summary>
        /// Do one-time initialization of the system.
        /// </summary>
        protected override void OnCreate()
        {
            try
            {
                Mod.log.Info($"{nameof(ProductionBalanceUISystem)}.{nameof(OnCreate)}");

                base.OnCreate();

                // Get other systems.
                _productionBalanceSystem = World.GetOrCreateSystemManaged<ProductionBalanceSystem>();

                // Initialize localization.
                _localizationManager = GameManager.instance.localizationManager;

                // Initialize production balance infos.
                InitializeProductionBalanceInfos();

                // Add bindings for UI to C#.
                AddBinding(new TriggerBinding          (ModAssemblyInfo.Name, BindingNameProductionBalanceButtonClicked, ProductionBalanceButtonClicked));
                AddBinding(new TriggerBinding<int, int>(ModAssemblyInfo.Name, BindingNameProductionBalancePanelMoved,    ProductionBalancePanelMoved   ));

                // Add bindings for C# to UI.
                AddBinding(_bindingProductionBalanceUISettings     = new RawValueBinding(ModAssemblyInfo.Name, BindingNameProductionBalanceUISettings,     WriteProductionBalanceUISettings));
                AddBinding(_bindingProductionBalanceInfoIndustrial = new RawValueBinding(ModAssemblyInfo.Name, BindingNameProductionBalanceInfoIndustrial, WriteProductionBalanceInfoIndustrial));
                AddBinding(_bindingProductionBalanceInfoOffice     = new RawValueBinding(ModAssemblyInfo.Name, BindingNameProductionBalanceInfoOffice,     WriteProductionBalanceInfoOffice));
            }
            catch(Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Called by the game when a GameMode is done being loaded.
        /// </summary>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
	        base.OnGameLoadingComplete(purpose, mode);

            // Initialize or deinitialize based on game mode.
            if (mode == GameMode.Game)
            {
                Initialize();
            }
            else
            {
                Deinitialize();
            }
        }

        /// <summary>
        /// Initialize this mod to get everything ready for a newly started game.
        /// </summary>
        private void Initialize()
        {
            try
            {
                Mod.log.Info($"{nameof(ProductionBalanceUISystem)}.{nameof(Initialize)}");

                // Initialize timing control.
                _previousRealTimeTicks = DateTime.Now.Ticks;

                // Initialize production balance infos.
                InitializeProductionBalanceInfos();
                
                // Update panel in UI.
                UpdateProductionBalanceUISettings();

                // Enable production balance activation key.
                ProxyAction activationKeyAction = Mod.ModSettings.GetAction(ModSettings.ProductionBalanceActivationKeyActionName);
                activationKeyAction.shouldBeEnabled = true;
                activationKeyAction.onInteraction += ActivationKeyInteraction;

                // Listen for language change events.
                _localizationManager.onActiveDictionaryChanged += LocalizationManager_onActiveDictionaryChanged;
            }
            catch(Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Deinitialize this mod to cleanup from a game just ended.
        /// </summary>
        private void Deinitialize()
        {
            try
            {
                Mod.log.Info($"{nameof(ProductionBalanceUISystem)}.{nameof(Deinitialize)}");

                // Disable production balance activation key.
                ProxyAction activationKeyAction = Mod.ModSettings.GetAction(ModSettings.ProductionBalanceActivationKeyActionName);
                activationKeyAction.shouldBeEnabled = false;
                activationKeyAction.onInteraction -= ActivationKeyInteraction;

                // Stop listening for language change events.
                _localizationManager.onActiveDictionaryChanged -= LocalizationManager_onActiveDictionaryChanged;
            }
            catch(Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Initialize production balance infos.
        /// </summary>
        private void InitializeProductionBalanceInfos()
        {
            _productionBalanceInfoIndustrial = new(true);
            _productionBalanceInfoOffice     = new(false);
        }

        /// <summary>
        /// Called every frame, even when at the main menu.
        /// </summary>
        protected override void OnUpdate()
        {
            base.OnUpdate();

            // Skip if not in a game.
            if (GameManager.instance.gameMode != GameMode.Game)
            {
                return;
            }

            try
            {
                // Panel must be visible.
                if (Mod.ModSettings.ProductionBalancePanelVisible)
                {
                    // Check for 1 second elapsed.
                    long currentRealTimeTicks = DateTime.Now.Ticks;
                    if (currentRealTimeTicks - _previousRealTimeTicks >= TimeSpan.TicksPerSecond)
                    {
                        // Set previous real time ticks.
                        _previousRealTimeTicks = currentRealTimeTicks;

                        // Get production balance infos and data.
                        _productionBalanceSystem.GetProductionBalanceInfos(
                            out _productionBalanceInfoIndustrial,
                            out _productionBalanceInfoOffice);

                        // Send production balance infos to UI.
                        _bindingProductionBalanceInfoIndustrial.Update();
                        _bindingProductionBalanceInfoOffice    .Update();
                    }
                }
            }
            catch (Exception ex) 
            {
                Mod.log.Error(ex);
            }
        }
        
        /// <summary>
        /// Write production balance settings that affect the UI to the UI.
        /// </summary>
        private void WriteProductionBalanceUISettings(IJsonWriter writer)
        {
			writer.TypeBegin(ModAssemblyInfo.Name + ".ProductionBalanceUISettings");
			writer.PropertyName("hideActivationButton");
			writer.Write(Mod.ModSettings.ProductionBalanceHideActivationButton);
			writer.PropertyName("activationKey");
            Mod.ModSettings.ProductionBalanceActivationKey.Write(writer);
			writer.PropertyName("panelVisible");
			writer.Write(Mod.ModSettings.ProductionBalancePanelVisible);
			writer.PropertyName("panelPositionX");
			writer.Write(Mod.ModSettings.ProductionBalancePanelPositionX);
			writer.PropertyName("panelPositionY");
			writer.Write(Mod.ModSettings.ProductionBalancePanelPositionY);
			writer.TypeEnd();
        }

        /// <summary>
        /// Write production balance info to the UI for industrial.
        /// </summary>
        private void WriteProductionBalanceInfoIndustrial(IJsonWriter writer)
        {
            _productionBalanceInfoIndustrial.Write(writer);
        }
        
        /// <summary>
        /// Write production balance info to the UI for office.
        /// </summary>
        private void WriteProductionBalanceInfoOffice(IJsonWriter writer)
        {
            _productionBalanceInfoOffice.Write(writer);
        }

        /// <summary>
        /// Handle localization active dictionary (i.e. language) change.
        /// </summary>
        private void LocalizationManager_onActiveDictionaryChanged()
        {
            // If production balance panel is visible, immediately update data.
            if (Mod.ModSettings.ProductionBalancePanelVisible)
            {
                _previousRealTimeTicks = 0;
            }
        }

        /// <summary>
        /// Handle activation key interaction.
        /// </summary>
        private void ActivationKeyInteraction(ProxyAction action, InputActionPhase phase)
        {
            // Activation key performed is same as production balance button clicked.
            if (phase == InputActionPhase.Performed)
            {
                ProductionBalanceButtonClicked();
            }
        }
        
        /// <summary>
        /// Event callback for production balance button clicked.
        /// </summary>
        private void ProductionBalanceButtonClicked()
        {
            // Toggle production balance panel visibility.
            Mod.ModSettings.ProductionBalancePanelVisible = !Mod.ModSettings.ProductionBalancePanelVisible;

            // If production balance panel is now visible, immediately update data.
            if (Mod.ModSettings.ProductionBalancePanelVisible)
            {
                _previousRealTimeTicks = 0;
            }

            // Send new visbility back to UI.
            UpdateProductionBalanceUISettings();
        }

        /// <summary>
        /// Event callback for production balance panel moved.
        /// </summary>
        private void ProductionBalancePanelMoved(int positionX, int positionY)
        {
            // Save production balance panel position.
            Mod.ModSettings.ProductionBalancePanelPositionX = positionX;
            Mod.ModSettings.ProductionBalancePanelPositionY = positionY;

            // Send position back to UI.
            UpdateProductionBalanceUISettings();
        }

        /// <summary>
        /// Update production balance UI settings.
        /// </summary>
        public void UpdateProductionBalanceUISettings()
        {
            _bindingProductionBalanceUISettings.Update();
        }
    }
}
