/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'export',
  basePath: '/task',
  trailingSlash: true,
  images: { unoptimized: true },
};

export default nextConfig;

