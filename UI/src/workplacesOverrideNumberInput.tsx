import { CSSProperties, useEffect, useState } from "react";

import { trigger                            } from "cs2/api";

import   mod                                  from "../mod.json";
import { ModuleResolver                     } from "moduleResolver";
import   styles                               from "workplacesOverrideNumberInput.module.scss";

// Custom input for entering a workplaces override number.
// Logic adapted from Advanced Building Control mod for number input.
export const WorkplacesOverrideNumberInput = ({value}: {value: number}) =>
{
    // Use state for the text representation of the value.
    const [rawTextValue, setRawTextValue] = useState<string>(`${value}`);
    useEffect(() =>
    {
        setRawTextValue(`${value}`);
    }, [value]);

    // Use state for whether or not the value is valid.
    const [valueIsValid, setValueIsValid] = useState<boolean>(true);

    // Handle change in input text.
    const onTextChange = (newTextValue: string) =>
    {
        // Validate input text.
        const parsedNumber: number = Number.parseFloat(newTextValue);
        if (!Number.isNaN(parsedNumber) &&
            Number.isFinite(parsedNumber) &&
            Number.isInteger(parsedNumber) &&
            parsedNumber >= 5 &&
            parsedNumber <= 9999)
        {
            // Value is valid.
            setValueIsValid(true);
            trigger(mod.id, "WorkplacesOverrideValidChanged", true);
            trigger(mod.id, "WorkplacesOverrideValueChanged", parsedNumber);
        }
        else
        {
            // Value is invalid.
            setValueIsValid(false);
            trigger(mod.id, "WorkplacesOverrideValidChanged", false);
        }

        // Always set the edit value.
        setRawTextValue(newTextValue);
    }

    // Style when number text is valid.
    const validStyle: Partial<CSSProperties> =
    {
        borderColor: "rgba(153, 153, 153, 1)",
    }

    // Style when number text is invalid.
    const errorStyle: Partial<CSSProperties> =
    {
        borderColor: "rgba(255, 0, 0, 1)",
    }

    // Construct the number input.
    return (
        <div className={`${ModuleResolver.instance.EllipsisTextInputTheme.wrapper}
                         ${styles.workplacesOverrideNumberInputWrapper}`}>
            <div
                className={`${ModuleResolver.instance.EllipsisTextInput.container}
                            ${ModuleResolver.instance.SIPTextInput.container}
                            ${styles.workplacesOverrideNumberInputContainer}`}
            >
                <input
                    className={`${ModuleResolver.instance.EllipsisTextInput.input} 
                                ${ModuleResolver.instance.SIPTextInput.input}
                                ${styles.workplacesOverrideNumberInput}`}
                    style={valueIsValid ? validStyle : errorStyle}
                    maxLength={4}
                    type="text"
                    value={rawTextValue}
                    onKeyDown={e =>
                    {
                        // Check for keys to force lose focus (i.e. blur = lose focus).
                        if (e.key === "Enter" || e.key === "Escape" || e.key === "Tab")
                        {
                            e.currentTarget.blur();
                        }
                    }}
                    onInput={e => e.currentTarget.value = e.currentTarget.value.replace(/[^0-9]/g, '')}
                    onChange={e => onTextChange(e.currentTarget.value)}
                />
            </div>
        </div>
    );
}
