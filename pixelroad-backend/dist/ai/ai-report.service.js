"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __metadata = (this && this.__metadata) || function (k, v) {
    if (typeof Reflect === "object" && typeof Reflect.metadata === "function") return Reflect.metadata(k, v);
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.AiReportService = void 0;
const common_1 = require("@nestjs/common");
const landmark_service_1 = require("../landmark/landmark.service");
let AiReportService = class AiReportService {
    landmarkService;
    constructor(landmarkService) {
        this.landmarkService = landmarkService;
    }
    async analysis(visitedLandmarks) {
        const landmarks = [];
        for (const visitedLandmark of visitedLandmarks) {
            const landmark = await this.landmarkService.getLandmarkById(visitedLandmark.landmarkId);
            if (landmark) {
                landmarks.push(landmark);
            }
        }
        return landmarks;
    }
};
exports.AiReportService = AiReportService;
exports.AiReportService = AiReportService = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [landmark_service_1.LandmarkService])
], AiReportService);
//# sourceMappingURL=ai-report.service.js.map