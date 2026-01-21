import { useDashboardData } from '../hooks/useDashboardData';

const DashboardPage = () => {
    const { summary, yoy, seasonal, loading, error } = useDashboardData();

    if (loading) return <div style={dashStyles.centered}>Loading metrics...</div>;
    if (error) return <div style={dashStyles.errorBanner}>{error}</div>;

    // Helper to get month name
    const getMonthName = (monthNum: number) => {
        return new Date(0, monthNum - 1).toLocaleString('default', { month: 'short' });
    };

    return (
        <div style={dashStyles.container}>
            <div style={dashStyles.header}>
                <h1 style={dashStyles.title}>Analytics Overview</h1>
                <p style={{ color: 'var(--text-muted)' }}>Real-time performance across all publishing channels.</p>
            </div>

            {/* Top Stats Cards */}
            <div style={dashStyles.kpiGrid}>
                <div className="card" style={dashStyles.statCard}>
                    <div style={dashStyles.iconCircle}>💰</div>
                    <div>
                        <p style={dashStyles.label}>Total Revenue</p>
                        <h2 style={dashStyles.value}>${summary?.totalRevenue.toLocaleString(undefined, { minimumFractionDigits: 2 })}</h2>
                    </div>
                </div>
                <div className="card" style={dashStyles.statCard}>
                    <div style={dashStyles.iconCircle}>📚</div>
                    <div>
                        <p style={dashStyles.label}>Books Published</p>
                        <h2 style={dashStyles.value}>{summary?.totalBooksPublished}</h2>
                    </div>
                </div>
                <div className="card" style={dashStyles.statCard}>
                    <div style={dashStyles.iconCircle}>📈</div>
                    <div>
                        <p style={dashStyles.label}>Transactions</p>
                        <h2 style={dashStyles.value}>{summary?.totalSalesTransactions.toLocaleString()}</h2>
                    </div>
                </div>
            </div>

            {/* Middle Section: Growth and Top Performers */}
            <div style={dashStyles.bottomGrid}>
                <div className="card" style={dashStyles.card}>
                    <h3 style={dashStyles.sectionTitle}>Business Growth</h3>
                    <div style={{...dashStyles.growthValue, color: yoy?.growth >= 0 ? '#10b981' : '#ef4444'}}>
                        {yoy?.growth >= 0 ? '↑' : '↓'} {(Math.abs(yoy?.growth || 0) * 100).toFixed(1)}% 
                        <span style={dashStyles.subLabel}>vs last year</span>
                    </div>
                    
                    <div style={dashStyles.miniList}>
                        <div style={dashStyles.listItem}>
                            <span>🏆 Top Performing Book</span>
                            <strong>{summary?.topPerformingBook || 'N/A'}</strong>
                        </div>
                        <div style={dashStyles.listItem}>
                            <span>🏪 Primary Platform</span>
                            <strong>{summary?.topPerformingPlatform || 'N/A'}</strong>
                        </div>
                    </div>
                </div>

                {/* Seasonal Revenue Visualization */}
                <div className="card" style={dashStyles.card}>
                    <h3 style={dashStyles.sectionTitle}>Monthly Revenue Trend</h3>
                    <div style={dashStyles.tableWrapper}>
                        {seasonal?.map(s => {
                            // Simple visual logic to create a "bar" effect
                            const maxRev = Math.max(...(seasonal?.map(m => m.totalRevenue) || [1]));
                            const barWidth = (s.totalRevenue / maxRev) * 100;
                            
                            return (
                                <div key={s.month} style={dashStyles.tableRow}>
                                    <span style={{ width: '60px', fontWeight: '600' }}>{getMonthName(s.month)}</span>
                                    <div style={dashStyles.barContainer}>
                                        <div style={{ ...dashStyles.bar, width: `${barWidth}%` }} />
                                    </div>
                                    <strong style={{ marginLeft: '12px', minWidth: '80px', textAlign: 'right' }}>
                                        ${s.totalRevenue.toFixed(0)}
                                    </strong>
                                </div>
                            );
                        })}
                    </div>
                </div>
            </div>
        </div>
    );
};

const dashStyles = {
    container: { animation: 'fadeIn 0.4s ease' },
    header: { marginBottom: '2.5rem' },
    title: { fontSize: '1.875rem', fontWeight: '800', marginBottom: '0.5rem', color: '#0f172a' },
    kpiGrid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '1.5rem', marginBottom: '2rem' },
    statCard: { display: 'flex', alignItems: 'center', gap: '1.25rem', padding: '1.5rem' },
    iconCircle: { width: '48px', height: '48px', borderRadius: '12px', backgroundColor: '#f1f5f9', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1.25rem' },
    label: { color: '#64748b', fontSize: '0.875rem', fontWeight: '600', marginBottom: '0.25rem' },
    value: { fontSize: '1.75rem', fontWeight: '800', color: '#0f172a', margin: 0 },
    bottomGrid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(400px, 1fr))', gap: '1.5rem' },
    card: { padding: '2rem' },
    sectionTitle: { fontSize: '1rem', fontWeight: '700', marginBottom: '1.5rem', color: '#334155', textTransform: 'uppercase' as const, letterSpacing: '0.05em' },
    growthValue: { fontSize: '2.5rem', fontWeight: '800', marginBottom: '1rem' },
    subLabel: { fontSize: '0.9rem', color: '#94a3b8', marginLeft: '10px', fontWeight: '400' },
    miniList: { display: 'flex', flexDirection: 'column', gap: '1rem', borderTop: '1px solid #f1f5f9', paddingTop: '1.5rem' },
    listItem: { display: 'flex', justifyContent: 'space-between', fontSize: '0.95rem', color: '#475569' },
    tableWrapper: { display: 'flex', flexDirection: 'column', gap: '8px' },
    tableRow: { display: 'flex', alignItems: 'center', padding: '4px 0' },
    barContainer: { flex: 1, height: '8px', backgroundColor: '#f1f5f9', borderRadius: '4px', overflow: 'hidden' },
    bar: { height: '100%', backgroundColor: '#38bdf8', borderRadius: '4px' },
    centered: { padding: '10rem', textAlign: 'center' as const, color: '#64748b', fontSize: '1.1rem' },
    errorBanner: { padding: '1.5rem', backgroundColor: '#fef2f2', color: '#991b1b', borderRadius: '12px', border: '1px solid #fee2e2' }
};

export default DashboardPage;