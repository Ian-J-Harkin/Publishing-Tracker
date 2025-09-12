import { useAuth } from '../context/AuthContext';
import { useNavigate } from 'react-router-dom';
import { useDashboardData } from '../hooks/useDashboardData';

const DashboardPage = () => {
    const { logout } = useAuth();
    const navigate = useNavigate();
    const { summary, yoy, seasonal, loading, error } = useDashboardData();

    const handleLogout = () => {
        logout();
        navigate('/login');
    };

    if (loading) {
        return <div>Loading...</div>;
    }

    if (error) {
        return <div>{error}</div>;
    }

    return (
        <div className="dashboard-container">
            <header className="dashboard-header">
                <h2>Dashboard</h2>
                <button onClick={handleLogout} className="logout-button">Logout</button>
            </header>
            {summary && (
                <div className="summary-grid">
                    <div className="summary-card"><h3>Total Revenue</h3><p>${summary.totalRevenue.toFixed(2)}</p></div>
                    <div className="summary-card"><h3>Total Books Published</h3><p>{summary.totalBooksPublished}</p></div>
                    <div className="summary-card"><h3>Total Sales Transactions</h3><p>{summary.totalSalesTransactions}</p></div>
                    <div className="summary-card"><h3>Top Performing Book</h3><p>{summary.topPerformingBook}</p></div>
                    <div className="summary-card"><h3>Top Performing Platform</h3><p>{summary.topPerformingPlatform}</p></div>
                </div>
            )}
            {yoy && (
                <div className="summary-card">
                    <h3>Year-Over-Year Growth</h3>
                    <p>Current Year Revenue: ${yoy.currentYearRevenue.toFixed(2)}</p>
                    <p>Previous Year Revenue: ${yoy.previousYearRevenue.toFixed(2)}</p>
                    <p>Growth: {(yoy.growth * 100).toFixed(2)}%</p>
                </div>
            )}
            {seasonal && (
                <div className="summary-card">
                    <h3>Seasonal Performance</h3>
                    <ul>
                        {seasonal.map((s: any) => (
                            <li key={s.month}>Month {s.month}: ${s.totalRevenue.toFixed(2)}</li>
                        ))}
                    </ul>
                </div>
            )}
        </div>
    );
};

export default DashboardPage;