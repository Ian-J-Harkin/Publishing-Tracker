import { useQuery } from '@tanstack/react-query';
import { dashboardService } from '../services/dashboardService';

export const useDashboardData = () => {
    const { data, isLoading, isError } = useQuery({
        queryKey: ['dashboardAll'],
        queryFn: dashboardService.getDashboardData
    });

    return {
        summary: data?.summary,
        yoy: data?.yoY,
        seasonal: data?.seasonal,
        loading: isLoading,
        error: isError ? 'Inconsistent data received from analytics engine.' : null
    };
};