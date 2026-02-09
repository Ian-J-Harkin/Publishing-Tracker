import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { bookService } from '../services/bookService';
import { BookPerformance } from '../types/book';
import { CSVLink } from 'react-csv';

const BookPerformancePage = () => {
    const { id } = useParams<{ id: string }>();
    const [performanceData, setPerformanceData] = useState<BookPerformance[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchPerformanceData = async () => {
            try {
                if (id) {
                    const data = await bookService.getBookPerformance(parseInt(id, 10));
                    setPerformanceData(data);
                }
            } catch {
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
            <h1 style={{ color: '#1e293b' }}>Book Performance</h1>
            <CSVLink data={performanceData} filename={"book-performance.csv"}>
                Export to CSV
            </CSVLink>
            <ul>
                {performanceData.map(p => (
                    <li key={`${p.platformName}-${p.currency}`}>
                        {p.platformName}: {p.currency} {p.totalRevenue.toFixed(2)}
                    </li>
                ))}
            </ul>
        </div>
    );
};

export default BookPerformancePage;