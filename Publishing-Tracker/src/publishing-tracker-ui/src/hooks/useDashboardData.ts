import { useQuery } from '@tanstack/react-query';
import { dashboardService } from '../services/dashboardService';

export const useDashboardData = () => {
    const { data: summary, isLoading: isSummaryLoading, isError: isSummaryError } = useQuery({
        queryKey: ['dashboardSummary'],
        queryFn: dashboardService.getDashboardSummary
    });

    const { data: yoy, isLoading: isYoyLoading, isError: isYoyError } = useQuery({
        queryKey: ['yoyComparison'],
        queryFn: dashboardService.getYoYComparison
    });

    const { data: seasonal, isLoading: isSeasonalLoading, isError: isSeasonalError } = useQuery({
        queryKey: ['seasonalPerformance'],
        queryFn: dashboardService.getSeasonalPerformance
    });

    return {
        summary,
        yoy,
        seasonal,
        loading: isSummaryLoading || isYoyLoading || isSeasonalLoading,
        error: isSummaryError || isYoyError || isSeasonalError
    };
};