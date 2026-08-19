import { useQuery } from '@tanstack/react-query';
import { useDependencies } from '../context/DependencyContext';

export const useDashboardData = () => {
    const { dashboard: dashboardServicePort } = useDependencies();
    const { data, isLoading, isError } = useQuery({
        queryKey: ['dashboardAll'],
        queryFn: dashboardServicePort.getDashboardData
    });

    return {
        summary: data?.summary,
        yoy: data?.yoY,
        seasonal: data?.seasonal,
        loading: isLoading,
        error: isError ? 'Inconsistent data received from analytics engine.' : null
    };
};
