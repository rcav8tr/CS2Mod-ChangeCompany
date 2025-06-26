import { bindValue, useValue, trigger           } from "cs2/api";
import { Dropdown, DropdownItem, DropdownToggle } from "cs2/ui";

import { CompanyResourceData                    } from "changeCompanyComponent";
import   styles                                   from "companySelector.module.scss";
import   mod                                      from "../mod.json";
import { ModuleResolver                         } from "moduleResolver";
import { ResourceIconLabel                      } from "resourceIconLabel";

// Define binding.
const bindingSelectedCompanyIndex = bindValue<number>(mod.id, "SelectedCompanyIndex", 0);

// Define props for company selector dropdown.
type CompanySelectorProps =
    {
        companyResourceDatas: CompanyResourceData[]
    }

// Custom dropdown for selecting a company.
export const CompanySelector = (props: CompanySelectorProps) =>
{
    // Get the value from binding.
    const selectedCompanyIndex: number = useValue(bindingSelectedCompanyIndex);

    // Function to join classes.
    function joinClasses(...classes: any) { return classes.join(" "); }

    // Create a dropdown item for each company and get content of the selected item.
    let selectedCompanyDropdownItemContent: JSX.Element = <></>;
    const noResource: string = "NoResource";
    let companyInfoCounter: number = 0;
    const companyDropdownItems: JSX.Element[] = props.companyResourceDatas.map
        (
            (companyResourceData: CompanyResourceData) =>
            {
                // Get company resources.
                const resourceOutput: string = companyResourceData.resourceOutput;
                const resourceInput1: string = companyResourceData.resourceInput1;
                const resourceInput2: string = companyResourceData.resourceInput2;

                // A company always has an output resource.
                // Get whether or not there are input resources.
                const hasInput1: boolean = resourceInput1 != noResource;
                const hasInput2: boolean = resourceInput2 != noResource;

                // Check if this company info is for the selected company.
                const selected: boolean = (companyInfoCounter == selectedCompanyIndex);

                // Get company info index.
                // Cannot use companyInfoCounter directly because its value changes for each entry.
                // Using companyInfoCounter directly results in all dropdown entires having the same index value as the last one.
                const companyInfoIndex: number = companyInfoCounter;

                // Construct dropdown item content.
                // Left always has the output resource.
                // Right has nothing, input1, or input1+input2 resources.
                const dropdownItemContent: JSX.Element =
                    <div className={styles.companyDropdownRow}>
                        <div className={joinClasses(ModuleResolver.instance.InfoRowClasses.left, styles.companyDropdownItemLeft)}>
                            <ResourceIconLabel resource={resourceOutput} trailingConcatenator={false} />
                        </div>
                        {hasInput1 &&
                            <div className={ModuleResolver.instance.InfoRowClasses.right}>
                                <ResourceIconLabel resource={resourceInput1} trailingConcatenator={hasInput2} />
                                {hasInput2 &&
                                    <ResourceIconLabel resource={resourceInput2} trailingConcatenator={false} />
                                }
                            </div>
                        }
                    </div>

                // Save content for selected company.
                if (selected)
                {
                    selectedCompanyDropdownItemContent = dropdownItemContent;
                }

                // Build dropdown item.
                // Don't know what the value property is used for, but it is required and an empty string seems to work.
                const dropdownItem: JSX.Element =
                    <DropdownItem
                        theme={ModuleResolver.instance.DropdownClasses}
                        value=""
                        closeOnSelect={true}
                        selected={selected}
                        className={styles.companyDropdownItem}
                        onChange={() => trigger(mod.id, "SelectedCompanyChanged", companyInfoIndex)}
                        focusKey={ModuleResolver.instance.FOCUS_DISABLED}
                    >
                        {dropdownItemContent}
                    </DropdownItem>

                // Increment company info counter for next one.
                companyInfoCounter++;

                // Return the dropdown item.
                return (dropdownItem);
            }
        );

    // Create the dropdown of companies.
    // The DropdownToggle shows the current selection and is the thing the user clicks on to show the dropdown list.
    return (
        <Dropdown
            theme={ModuleResolver.instance.DropdownClasses}
            content={companyDropdownItems}
            focusKey={ModuleResolver.instance.FOCUS_DISABLED}
        >
            <DropdownToggle>
                {selectedCompanyDropdownItemContent}
            </DropdownToggle>
        </Dropdown>
    );
}