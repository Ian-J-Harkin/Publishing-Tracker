import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { bookService } from '../services/bookService';
import { CSVLink } from 'react-csv';

const BookPerformancePage = () => {
    const { id } = useParams<{ id: string }>();
    const [performanceData, setPerformanceData] = useState<any[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchPerformanceData = async () => {
            try {
                if (id) {
                    const data = await bookService.getBookPerformance(parseInt(id, 10));
                    setPerformanceData(data);
                }
            } catch (err) {
                setError('Failed to fetch book performance data.');
            } finally {
                setLoading(false);
            }
        };

        fetchPerformanceData();
    }, [id]);

    if (loading) {
        return <div>Loading...</div>;
    }

    if (error) {
        return <div>{error}</div>;
    }

    return (
        <div className="form-container">
            <h1>Book Performance</h1>
            <CSVLink data={performanceData} filename={"book-performance.csv"}>
                Export to CSV
            </CSVLink>
            <ul>
                {performanceData.map(p => (
                    <li key={p.platformName}>
                        {p.platformName}: ${p.totalRevenue.toFixed(2)}
                    </li>
                ))}
            </ul>
        </div>
    );
};

export default BookPerformancePage;