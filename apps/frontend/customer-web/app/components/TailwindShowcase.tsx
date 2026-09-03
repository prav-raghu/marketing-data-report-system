export function TailwindShowcase() {
    return (
        <div className="bg-gradient-to-br from-indigo-500 via-purple-500 to-pink-500 rounded-lg shadow-lg p-6 text-white border-2 border-yellow-400">
            <h2 className="text-2xl font-bold mb-4">🎨 Tailwind CSS Showcase</h2>
            <div className="space-y-3">
                <div className="flex items-center gap-2">
                    <div className="w-4 h-4 bg-red-500 rounded-full animate-pulse"></div>
                    <span className="text-sm">Gradient backgrounds ✅</span>
                </div>
                <div className="flex items-center gap-2">
                    <div className="w-4 h-4 bg-green-500 rounded-full animate-bounce"></div>
                    <span className="text-sm">Animations ✅</span>
                </div>
                <div className="flex items-center gap-2">
                    <div className="w-4 h-4 bg-blue-500 rounded-full"></div>
                    <span className="text-sm">Responsive design ✅</span>
                </div>
                <div className="flex items-center gap-2">
                    <div className="w-4 h-4 bg-yellow-500 rounded-full"></div>
                    <span className="text-sm">Utility classes ✅</span>
                </div>
                <div className="grid grid-cols-3 gap-2 mt-4">
                    <div className="h-12 bg-white/20 rounded backdrop-blur-sm"></div>
                    <div className="h-12 bg-white/30 rounded backdrop-blur-sm"></div>
                    <div className="h-12 bg-white/40 rounded backdrop-blur-sm"></div>
                </div>
            </div>
        </div>
    );
}
