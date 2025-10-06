import { bindValue } from "cs2/api";

import mod from "../mod.json";

// Define production balance UI settings.
export type ProductionBalanceUISettings =
{
    hideActivationButton:       boolean;
    activationKey:              any;
    panelVisible:               boolean;
    panelPositionX:             number;
    panelPositionY:             number;
}

// Define production balance info.
// All values are strings formatted in C#.
export type ProductionBalanceInfo =
{
    companyCount:               string;
    minimumCompanies:           string;

    standardDeviationPercent:   string;
    minimumStandardDeviation:   string;

    lastChangeDateTime:         string;
    lastChangeFromResource:     string;
    lastChangeToResource:       string;

    nextCheckDateTime:          string;
}

// Define binding for selected company.
export const bindingSelectedCompanyIndex            = bindValue<number                      >(mod.id, "SelectedCompanyIndex",   0);

// Define binding for company locked.
export const bindingCompanyLocked                   = bindValue<boolean                     >(mod.id, "CompanyLocked",          false);

// Define bindings for production balance.
export const bindingProductionBalanceUISettings     = bindValue<ProductionBalanceUISettings >(mod.id, "ProductionBalanceUISettings");
export const bindingProductionBalanceInfoIndustrial = bindValue<ProductionBalanceInfo       >(mod.id, "ProductionBalanceInfoIndustrial");
export const bindingProductionBalanceInfoOffice     = bindValue<ProductionBalanceInfo       >(mod.id, "ProductionBalanceInfoOffice");
