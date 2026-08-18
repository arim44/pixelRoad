"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.LandmarkRecommendationService = void 0;
const common_1 = require("@nestjs/common");
let LandmarkRecommendationService = class LandmarkRecommendationService {
    selectRecommendation(visitedLandmarks, allLandmarks) {
        const visitedIds = new Set(visitedLandmarks.map((landmark) => landmark.landmarkId));
        const unvisited = allLandmarks.filter((landmark) => !visitedIds.has(landmark.id));
        if (unvisited.length === 0) {
            return undefined;
            console.log("추천할 랜드마크가 없습니다");
        }
        const categoryVisitCounts = new Map();
        for (const visited of visitedLandmarks) {
            const landmark = allLandmarks.find((landmark) => landmark.id === visited.landmarkId);
            if (!landmark)
                continue;
            const currentCount = categoryVisitCounts.get(landmark.category) ?? 0;
            categoryVisitCounts.set(landmark.category, currentCount + visited.visitCount);
        }
        const categoryOrder = [...categoryVisitCounts.entries()]
            .sort((a, b) => b[1] - a[1]);
        for (const [category] of categoryOrder) {
            const candidates = unvisited.filter((landmark) => landmark.category === category);
            if (candidates.length > 0) {
                const randomIndex = Math.floor(Math.random() * candidates.length);
                return candidates[randomIndex];
            }
        }
        return undefined;
    }
};
exports.LandmarkRecommendationService = LandmarkRecommendationService;
exports.LandmarkRecommendationService = LandmarkRecommendationService = __decorate([
    (0, common_1.Injectable)()
], LandmarkRecommendationService);
//# sourceMappingURL=landmark-recommendation.service.js.map