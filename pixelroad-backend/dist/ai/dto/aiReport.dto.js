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
exports.AiReportApiDto = exports.AiReportResponseDto = exports.RecommendationDto = exports.CreateReportDto = exports.VisitedLandmarkDto = void 0;
const swagger_1 = require("@nestjs/swagger");
const class_transformer_1 = require("class-transformer");
const class_validator_1 = require("class-validator");
class VisitedLandmarkDto {
    landmarkId;
    visitCount;
}
exports.VisitedLandmarkDto = VisitedLandmarkDto;
__decorate([
    (0, swagger_1.ApiProperty)({ example: 2, description: "랜드마크 ID" }),
    (0, class_validator_1.IsInt)(),
    (0, class_validator_1.IsNumber)(),
    __metadata("design:type", Number)
], VisitedLandmarkDto.prototype, "landmarkId", void 0);
__decorate([
    (0, swagger_1.ApiProperty)({ example: 2, description: "총 방문 횟수" }),
    (0, class_validator_1.IsInt)(),
    (0, class_validator_1.Min)(1),
    __metadata("design:type", Number)
], VisitedLandmarkDto.prototype, "visitCount", void 0);
class CreateReportDto {
    visitedLandmarks;
}
exports.CreateReportDto = CreateReportDto;
__decorate([
    (0, swagger_1.ApiProperty)({
        type: [VisitedLandmarkDto],
        description: "사용자의 방문 랜드마크 목록"
    }),
    (0, class_validator_1.IsArray)(),
    (0, class_validator_1.ValidateNested)({ each: true }),
    (0, class_transformer_1.Type)(() => VisitedLandmarkDto),
    __metadata("design:type", Array)
], CreateReportDto.prototype, "visitedLandmarks", void 0);
class RecommendationDto {
    landmarkId;
    name;
    reason;
}
exports.RecommendationDto = RecommendationDto;
__decorate([
    (0, swagger_1.ApiProperty)({ example: 24, description: "추천 랜드마크 ID" }),
    __metadata("design:type", Number)
], RecommendationDto.prototype, "landmarkId", void 0);
__decorate([
    (0, swagger_1.ApiProperty)({ example: "창덕궁", description: "추천 랜드마크 이름" }),
    __metadata("design:type", String)
], RecommendationDto.prototype, "name", void 0);
__decorate([
    (0, swagger_1.ApiProperty)({
        example: "조선 시대의 궁궐을 더 탐험해보세요",
        description: "추천 이유"
    }),
    __metadata("design:type", String)
], RecommendationDto.prototype, "reason", void 0);
class AiReportResponseDto {
    analysis;
    recommendation;
}
exports.AiReportResponseDto = AiReportResponseDto;
__decorate([
    (0, swagger_1.ApiProperty)({
        example: "궁궐과 역사 유적을 중심으로 탐험하는 성향입니다",
        description: "사용자의 탐험 성향분석"
    }),
    __metadata("design:type", String)
], AiReportResponseDto.prototype, "analysis", void 0);
__decorate([
    (0, swagger_1.ApiProperty)({
        type: RecommendationDto,
        description: "AI가 추천하는 랜드마크"
    }),
    __metadata("design:type", RecommendationDto)
], AiReportResponseDto.prototype, "recommendation", void 0);
class AiReportApiDto {
    success;
    data;
}
exports.AiReportApiDto = AiReportApiDto;
__decorate([
    (0, swagger_1.ApiProperty)({
        example: true,
        description: "성공 여부"
    }),
    __metadata("design:type", Boolean)
], AiReportApiDto.prototype, "success", void 0);
__decorate([
    (0, swagger_1.ApiProperty)({ type: AiReportResponseDto }),
    __metadata("design:type", AiReportResponseDto)
], AiReportApiDto.prototype, "data", void 0);
//# sourceMappingURL=aiReport.dto.js.map