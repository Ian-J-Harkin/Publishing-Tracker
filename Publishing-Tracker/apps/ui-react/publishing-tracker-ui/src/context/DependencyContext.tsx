import React, { createContext, useContext, ReactNode } from 'react';
import { IDependencies } from '../ports';
import { AuthAxiosAdapter } from '../adapters/AuthAxiosAdapter';
import { BookAxiosAdapter } from '../adapters/BookAxiosAdapter';
import { ImportAxiosAdapter } from '../adapters/ImportAxiosAdapter';
import { PlatformAxiosAdapter } from '../adapters/PlatformAxiosAdapter';
import { SaleAxiosAdapter } from '../adapters/SaleAxiosAdapter';
import { DashboardAxiosAdapter } from '../adapters/DashboardAxiosAdapter';
import { UserAxiosAdapter } from '../adapters/UserAxiosAdapter';

const defaultDependencies: IDependencies = {
    auth: AuthAxiosAdapter,
    books: BookAxiosAdapter,
    imports: ImportAxiosAdapter,
    platforms: PlatformAxiosAdapter,
    sales: SaleAxiosAdapter,
    dashboard: DashboardAxiosAdapter,
    users: UserAxiosAdapter,
};

const DependencyContext = createContext<IDependencies | undefined>(undefined);

export const DependencyProvider: React.FC<{ children: ReactNode, dependencies?: IDependencies }> = ({ children, dependencies = defaultDependencies }) => {
    return (
        <DependencyContext.Provider value={dependencies}>
            {children}
        </DependencyContext.Provider>
    );
};

export const useDependencies = (): IDependencies => {
    const context = useContext(DependencyContext);
    if (!context) {
        throw new Error('useDependencies must be used within a DependencyProvider');
    }
    return context;
};
