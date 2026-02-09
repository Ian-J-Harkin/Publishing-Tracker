import { useDashboardData } from '../hooks/useDashboardData';

const DashboardPage = () => {
    const { summary, yoy, seasonal, loading, error } = useDashboardData();

    if (loading) return <div style={dashStyles.centered}>
        <div className="shimmer" style={{ height: '200px', borderRadius: '16px', marginBottom: '2rem' }}></div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '2rem' }}>
            <div className="shimmer" style={{ height: '400px', borderRadius: '16px' }}></div>
            <div className="shimmer" style={{ height: '400px', borderRadius: '16px' }}></div>
        </div>
    </div>;

    if (error) return <div style={dashStyles.errorBanner}>{error}</div>;

    const getMonthName = (monthNum: number) => {
        return new Date(0, monthNum - 1).toLocaleString('default', { month: 'short' });
    };

    return (
        <div style={dashStyles.container}>
            <div style={dashStyles.header}>
                <h1 style={{ marginBottom: '0.5rem', color: '#1e293b' }}>Strategic Analytics</h1>
                <p style={{ color: 'var(--text-muted)', fontSize: '1.1rem' }}>Performance intelligence for your intellectual property.</p>
            </div>

            <div style={dashStyles.kpiGrid}>
                {/* Revenue Card - Premium Gradient */}
                <div className="card metric-card" style={dashStyles.statCard}>
                    <div style={dashStyles.iconBox}>💰</div>
                    <div style={{ flex: 1 }}>
                        <p style={dashStyles.label}>Cumulative Revenue</p>
                        {summary?.revenueByCurrency && summary.revenueByCurrency.length > 0 ? (
                            summary.revenueByCurrency.map(r => (
                                <h2 key={r.currency} style={dashStyles.value}>
                                    <span style={{ color: 'var(--primary)', marginRight: '8px' }}>{r.currency}</span>
                                    {r.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                                </h2>
                            ))
                        ) : (
                            <h2 style={dashStyles.value}>USD 0.00</h2>
                        )}
                    </div>
                </div>

                {/* Books Card */}
                <div className="card metric-card" style={dashStyles.statCard}>
                    <div style={{ ...dashStyles.iconBox, background: 'rgba(129, 140, 248, 0.1)' }}>📚</div>
                    <div>
                        <p style={dashStyles.label}>Active Portfolio</p>
                        <h2 style={dashStyles.value}>{summary?.totalBooksPublished} <span style={dashStyles.unit}>Titles</span></h2>
                    </div>
                </div>

                {/* Transactions Card */}
                <div className="card metric-card" style={dashStyles.statCard}>
                    <div style={{ ...dashStyles.iconBox, background: 'rgba(244, 114, 182, 0.1)' }}>📈</div>
                    <div>
                        <p style={dashStyles.label}>Market Volume</p>
                        <h2 style={dashStyles.value}>{summary?.totalSalesTransactions.toLocaleString()} <span style={dashStyles.unit}>Sales</span></h2>
                    </div>
                </div>
            </div>

            <div style={dashStyles.bottomGrid}>
                {/* Growth Card */}
                <div className="card" style={dashStyles.growthCard}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '2rem' }}>
                        <h3 style={dashStyles.sectionTitle}>Year-over-Year Growth</h3>
                        <div style={{
                            ...dashStyles.growthBadge,
                            backgroundColor: (yoy?.growth ?? 0) >= 0 ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
                            color: (yoy?.growth ?? 0) >= 0 ? 'var(--success)' : 'var(--danger)'
                        }}>
                            {(yoy?.growth ?? 0) >= 0 ? '↑' : '↓'} {(Math.abs(yoy?.growth || 0) * 100).toFixed(1)}%
                        </div>
                    </div>

                    <div style={dashStyles.miniList}>
                        <div style={dashStyles.listItem}>
                            <div style={dashStyles.listInfo}>
                                <div style={dashStyles.listLabel}>Top Revenue Title</div>
                                <div style={dashStyles.listValue}>{summary?.topPerformingBook || 'Acquiring data...'}</div>
                            </div>
                            <span style={dashStyles.listIcon}>🥇</span>
                        </div>
                        <div style={dashStyles.listItem}>
                            <div style={dashStyles.listInfo}>
                                <div style={dashStyles.listLabel}>Dominant Sales Channel</div>
                                <div style={dashStyles.listValue}>{summary?.topPerformingPlatform || 'Acquiring data...'}</div>
                            </div>
                            <span style={dashStyles.listIcon}>🏛️</span>
                        </div>
                    </div>
                </div>

                {/* Revenue Trend Visualization */}
                <div className="card" style={dashStyles.trendCard}>
                    <h3 style={dashStyles.sectionTitle}>Monthly Revenue Trajectory</h3>
                    <div style={dashStyles.tableWrapper}>
                        {seasonal?.map((s: { month: number; totalRevenue: number }) => {
                            const maxRev = Math.max(...(seasonal?.map((m: { totalRevenue: number }) => m.totalRevenue) || [1]));
                            const barHeight = (s.totalRevenue / (maxRev || 1)) * 100;

                            return (
                                <div key={s.month} style={dashStyles.trendColumn}>
                                    <div style={dashStyles.barWrapper}>
                                        <div style={{
                                            ...dashStyles.bar,
                                            height: `${Math.max(barHeight, 5)}%`,
                                            background: `linear-gradient(to top, var(--primary), var(--secondary))`
                                        }} />
                                    </div>
                                    <span style={dashStyles.monthLabel}>{getMonthName(s.month)}</span>
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
    container: { animation: 'fadeIn 0.6s cubic-bezier(0.16, 1, 0.3, 1)' },
    header: { marginBottom: '3rem' },
    kpiGrid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '2rem', marginBottom: '2.5rem' },
    statCard: { display: 'flex', alignItems: 'center', gap: '1.5rem', padding: '2rem' },
    iconBox: {
        width: '64px',
        height: '64px',
        borderRadius: '16px',
        backgroundColor: 'rgba(56, 189, 248, 0.1)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: '1.75rem',
        boxShadow: 'inset 0 0 0 1px rgba(255,255,255,0.05)'
    },
    label: { color: 'var(--text-muted)', fontSize: '0.9rem', fontWeight: '600', marginBottom: '0.5rem', textTransform: 'uppercase' as const, letterSpacing: '0.05em' },
    value: { fontSize: '2rem', fontWeight: '800', color: '#1e293b', margin: 0, letterSpacing: '-0.02em' },
    unit: { fontSize: '1rem', fontWeight: '500', color: 'var(--text-muted)', marginLeft: '4px' },
    bottomGrid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(450px, 1fr))', gap: '2rem' },
    growthCard: { padding: '2.5rem' },
    growthBadge: {
        padding: '0.5rem 1rem',
        borderRadius: '12px',
        fontSize: '1rem',
        fontWeight: '800',
        display: 'flex',
        alignItems: 'center'
    },
    sectionTitle: { fontSize: '1.1rem', fontWeight: '700', marginBottom: '1.5rem', color: '#1e293b' },
    miniList: { display: 'flex', flexDirection: 'column' as const, gap: '1.25rem' },
    listItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '1.25rem',
        backgroundColor: 'rgba(255,255,255,0.02)',
        borderRadius: '16px',
        border: '1px solid var(--border)'
    },
    listInfo: { display: 'flex', flexDirection: 'column' as const, gap: '2px' },
    listLabel: { fontSize: '0.75rem', color: 'var(--text-muted)', fontWeight: '600', textTransform: 'uppercase' as const },
    listValue: { fontSize: '1rem', fontWeight: '700', color: '#1e293b' },
    listIcon: { fontSize: '1.5rem' },
    trendCard: { padding: '2.5rem', display: 'flex', flexDirection: 'column' as const },
    tableWrapper: { display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', flex: 1, gap: '10px', minHeight: '240px' },
    trendColumn: { display: 'flex', flexDirection: 'column' as const, alignItems: 'center', flex: 1, gap: '12px' },
    barWrapper: { width: '100%', flex: 1, position: 'relative' as const, display: 'flex', alignItems: 'flex-end', justifyContent: 'center' },
    bar: { width: '100%', maxWidth: '30px', borderRadius: '6px 6px 2px 2px', transition: 'height 1s cubic-bezier(0.34, 1.56, 0.64, 1)' },
    monthLabel: { fontSize: '0.7rem', fontWeight: '700', color: 'var(--text-muted)', textTransform: 'uppercase' as const },
    centered: { padding: '5rem', textAlign: 'center' as const },
    errorBanner: { padding: '2rem', backgroundColor: 'rgba(239, 68, 68, 0.1)', color: 'var(--danger)', borderRadius: '16px', border: '1px solid var(--danger)' }
};

export default DashboardPage;