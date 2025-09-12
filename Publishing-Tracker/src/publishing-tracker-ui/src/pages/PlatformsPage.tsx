import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { platformService } from '../services/platformService';
import { Platform } from '../types/platform';

const PlatformsPage = () => {
    const [platforms, setPlatforms] = useState<Platform[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchPlatforms = async () => {
            try {
                const data = await platformService.getPlatforms();
                setPlatforms(data);
            } catch (err) {
                setError('Failed to fetch platforms');
            } finally {
                setLoading(false);
            }
        };

        fetchPlatforms();
    }, []);

    if (loading) {
        return <div>Loading...</div>;
    }

    if (error) {
        return <div>{error}</div>;
    }

    return (
        <div>
            <h1>Platforms</h1>
            <Link to="/platforms/request">Request New Platform</Link>
            <ul>
                {platforms.map(platform => (
                    <li key={platform.id}>
                        {platform.name} - {platform.baseUrl} - {platform.commissionRate}
                    </li>
                ))}
            </ul>
        </div>
    );
};

export default PlatformsPage;