import { Suspense, lazy } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';

const LoginPage = lazy(() => import('./pages/LoginPage'));
const RegisterPage = lazy(() => import('./pages/RegisterPage'));
const DashboardPage = lazy(() => import('./pages/DashboardPage'));
const BookListPage = lazy(() => import('./pages/BookListPage'));
const AddBookPage = lazy(() => import('./pages/AddBookPage'));
const EditBookPage = lazy(() => import('./pages/EditBookPage'));
const SalesPage = lazy(() => import('./pages/SalesPage'));
const AddSalePage = lazy(() => import('./pages/AddSalePage'));
const ImportPage = lazy(() => import('./pages/ImportPage'));
const ImportHistoryPage = lazy(() => import('./pages/ImportHistoryPage'));
const PlatformsPage = lazy(() => import('./pages/PlatformsPage'));
const RequestPlatformPage = lazy(() => import('./pages/RequestPlatformPage'));
const MainLayout = lazy(() => import('./components/layout/MainLayout'));

function App() {
    return (
        <Router>
            <Suspense fallback={<div>Loading...</div>}>
                <Routes>
                    <Route path="/" element={<Navigate to="/login" />} />
                    <Route path="/login" element={<LoginPage />} />
                    <Route path="/register" element={<RegisterPage />} />
                    <Route element={<MainLayout />}>
                        <Route path="/dashboard" element={<DashboardPage />} />
                        <Route path="/books" element={<BookListPage />} />
                        <Route path="/books/add" element={<AddBookPage />} />
                        <Route path="/books/edit/:id" element={<EditBookPage />} />
                        <Route path="/sales" element={<SalesPage />} />
                        <Route path="/sales/add" element={<AddSalePage />} />
                        <Route path="/import" element={<ImportPage />} />
                        <Route path="/import/history" element={<ImportHistoryPage />} />
                        <Route path="/platforms" element={<PlatformsPage />} />
                        <Route path="/platforms/request" element={<RequestPlatformPage />} />
                    </Route>
                </Routes>
            </Suspense>
        </Router>
    );
}

export default App;
