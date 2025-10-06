import { MouseEvent, CSSProperties } from "react";

import { trigger, useValue  } from "cs2/api";
import { useLocalization    } from "cs2/l10n";
import { Panel              } from "cs2/ui";

import { ProductionBalanceUISettings, bindingProductionBalanceUISettings    } from "bindings";
import   mod                                                                  from "../mod.json";
import { ModuleResolver                                                     } from "moduleResolver";
import   productionBalanceIcon                                                from "images/productionBalance.svg";
import   styles                                                               from "productionBalancePanel.module.scss";
import { ProductionBalancePanelInfo                                         } from "productionBalancePanelInfo";

// Panel to display industrial and office production balance infos.
export const ProductionBalancePanel = () =>
{
    // Define an element ID for each element that needs to be found by ID.
    // The prefix includes the Paradox username and mod name to avoid conflicts with elements from other mods.
    const elementIDPrefix: string = "rcav8tr-change-company-";
    const elementIDProductionBalancePanel:      string = elementIDPrefix + "production-balance-panel";
    const elementIDProductionBalancePanelClose: string = elementIDPrefix + "production-balance-panel-close";

    // Get production balance UI settings.
    const productionBalanceUISettings: ProductionBalanceUISettings = useValue(bindingProductionBalanceUISettings);

    // Get panel title.
    const { translate } = useLocalization();
    const productionBalanceTitle: string | null = translate(mod.id + ".ProductionBalanceStatistics");

    // Verify panel position.
    let verifiedPanelPosition = { x: productionBalanceUISettings.panelPositionX, y: productionBalanceUISettings.panelPositionY };
    const panel: HTMLElement | null = document.getElementById(elementIDProductionBalancePanel);
    if (panel)
    {
        // Prevent any part of panel from going outside the window.
        const panelRect = panel.getBoundingClientRect();
        verifiedPanelPosition = checkPositionOnWindow(
            productionBalanceUISettings.panelPositionX, productionBalanceUISettings.panelPositionY, panelRect.width, panelRect.height);

        // Check for any chanage in panel position.
        if (verifiedPanelPosition.x != productionBalanceUISettings.panelPositionX ||
            verifiedPanelPosition.y != productionBalanceUISettings.panelPositionY)
        {
            // Move panel to verified position.
            trigger(mod.id, "ProductionBalancePanelMoved", verifiedPanelPosition.x, verifiedPanelPosition.y);
        }
    }

    // Set panel to the verified position using a dynamic style.
    const panelStyle: Partial<CSSProperties> =
    {
        left:   verifiedPanelPosition.x + "px",
        top:    verifiedPanelPosition.y + "px",
    }

    // Function to join classes.
    function joinClasses(...classes: any) { return classes.join(" "); }

    // Variables for dragging.
    let productionBalancePanel: HTMLElement | null = null;
    let relativePositionX: number = 0.0;
    let relativePositionY: number = 0.0;

    // Start dragging.
    // Dragging is initiated by mouse down on the panel header, but it is the whole panel that is moved.
    function onMouseDown(e: MouseEvent<HTMLDivElement, globalThis.MouseEvent>)
    {
        // Ignore mouse down if other than left mouse button.
        if (e.button !== 0)
        {
            return;
        }

        // Get close button.
        const closeButton = document.getElementById(elementIDProductionBalancePanelClose);
        if (closeButton)
        {
            // Ignore mouse down if over the close button.
            const closeButtonRect = closeButton.getBoundingClientRect();
            if (e.clientX >= closeButtonRect.left && e.clientX <= closeButtonRect.left + closeButtonRect.width &&
                e.clientY >= closeButtonRect.top  && e.clientY <= closeButtonRect.top  + closeButtonRect.height)
            {
                return;
            }
        }

        // Get panel.
        productionBalancePanel = document.getElementById(elementIDProductionBalancePanel);
        if (productionBalancePanel)
        {
            // Save the position of the mouse relative to the panel.
            const panelRect = productionBalancePanel.getBoundingClientRect();
            relativePositionX = e.clientX - panelRect.left;
            relativePositionY = e.clientY - panelRect.top;

            // Add mouse event listeners.
            window.addEventListener("mousemove", onMouseMove);
            window.addEventListener("mouseup",   onMouseUp);

            // Stop event propagation.
            e.stopPropagation();
            e.preventDefault();
        }
    }

    // Move the panel while dragging.
    function onMouseMove(e: { clientX: number; clientY: number; stopPropagation: () => void; preventDefault: () => void; })
    {
        // Check if panel is valid.
        if (productionBalancePanel)
        {
            // Compute new panel position based on current mouse position.
            // Adjusting by relative position while dragging keeps the panel in the same location
            // under the pointer as when the panel was originally clicked to start dragging.
            const newPosition = { x: e.clientX - relativePositionX, y: e.clientY - relativePositionY };

            // Prevent any part of panel from going outside the window.
            const panelRect = productionBalancePanel.getBoundingClientRect();
            const checkedPosition = checkPositionOnWindow(newPosition.x, newPosition.y, panelRect.width, panelRect.height);

            // Move panel to checked position.
            productionBalancePanel.style.left = checkedPosition.x + "px";
            productionBalancePanel.style.top  = checkedPosition.y + "px";

            // Stop event propagation.
            e.stopPropagation();
            e.preventDefault();
        }
    }

    // Ensure element is not outside the window.
    function checkPositionOnWindow(positionX: number, positionY: number, elementWidth: number, elementHeight: number): { x: number, y: number }
    {
        // Check position against left and top.
        if (positionX < 0) { positionX = 0.0; }
        if (positionY < 0) { positionY = 0.0; }

        // Check position against right and bottom.
        if (positionX > window.innerWidth  - elementWidth ) { positionX = window.innerWidth  - elementWidth;  }
        if (positionY > window.innerHeight - elementHeight) { positionY = window.innerHeight - elementHeight; }

        // Return the checked position.
        return { x: positionX, y: positionY };
    }

    // Finish dragging.
    function onMouseUp(e: { stopPropagation: () => void; preventDefault: () => void; })
    {
        // Check if panel is valid.
        if (productionBalancePanel)
        {
            // Remove mouse event listeners.
            window.removeEventListener("mousemove", onMouseMove);
            window.removeEventListener("mouseup",   onMouseUp);

            // Trigger panel moved event.
            const panelRect = productionBalancePanel.getBoundingClientRect();
            trigger(mod.id, "ProductionBalancePanelMoved", panelRect.left, panelRect.top);

            // Stop event propagation.
            e.stopPropagation();
            e.preventDefault();
        }
    }

    // Handle click on close button.
    // Click on close button is same as click on activation button.
    function onCloseClick()
    {
        trigger("audio", "playSound", ModuleResolver.instance.UISound.selectItem, 1);
        trigger(mod.id, "ProductionBalanceButtonClicked")
    }

    // The panel is displayed only when the visibile value is true.
    // The panel consists of the header and content.
    // The header consists of an image, a div for the title, and a close button.
    // The content consists of 2 sections for production balance info for industrial and office.
    return (
        <>
        {
            productionBalanceUISettings.panelVisible &&
            (
                <Panel
                    id={elementIDProductionBalancePanel}
                    className={styles.productionBalancePanel}
                    style={panelStyle}
                    header={(
                        <div
                            className={styles.productionBalancePanelHeader}
                            onMouseDown={(e) => onMouseDown(e)}
                        >
                            <img className={ModuleResolver.instance.PanelClasses.icon} src={productionBalanceIcon} />
                            <div className={joinClasses(ModuleResolver.instance.PanelThemeClasses.title,
                                                        styles.productionBalancePanelHeaderTitle)}>
                                {productionBalanceTitle}
                            </div>
                            <button
                                id={elementIDProductionBalancePanelClose}
                                className={joinClasses(ModuleResolver.instance.PanelClasses.closeButton,
                                                       ModuleResolver.instance.RoundHighlightButtonClasses.button,
                                                       styles.productionBalancePanelHeaderClose)}
                                onClick={() => onCloseClick()}
                            >
                                <div
                                    className={joinClasses(ModuleResolver.instance.TintedIconClasses.tintedIcon,
                                                           ModuleResolver.instance.IconButtonClasses.icon)}
                                    style={{ maskImage: "url(Media/Glyphs/Close.svg)" }}
                                >
                                </div>
                            </button>
                        </div>
                    )}
                >
                    <ProductionBalancePanelInfo isIndustrial={true}  />
                    <ProductionBalancePanelInfo isIndustrial={false} />
                </Panel>
            )
        }
        </>
    )
}