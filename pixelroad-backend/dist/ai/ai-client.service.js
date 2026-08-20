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
exports.AiClientService = void 0;
const inference_1 = require("@huggingface/inference");
const common_1 = require("@nestjs/common");
const ai_report_prompt_constant_1 = require("./constants/ai-report-prompt.constant");
let AiClientService = class AiClientService {
    client;
    constructor() {
        const token = process.env.HF_TOKEN?.trim();
        if (!token) {
            throw new Error("HF_TOKEN 이 설정되지 않았습니다.");
        }
        this.client = new inference_1.InferenceClient(token);
    }
    async generateReport(userPrompt) {
        try {
            const response = await this.client.chatCompletion({
                model: process.env.HF_MODEL,
                messages: [
                    {
                        role: "system",
                        content: ai_report_prompt_constant_1.AI_REPORT_SYSTEM_PROMPTS.AIREPORT_ANALYSIS,
                    },
                    {
                        role: "user",
                        content: userPrompt
                    },
                ],
                temperature: 0.7,
                max_tokens: 700,
            });
            const choice = response.choices[0];
            if (!choice?.message) {
                throw new common_1.InternalServerErrorException("AI 응답 메시지가 없습니다.");
            }
            if (choice.finish_reason === "length") {
                throw new common_1.InternalServerErrorException("AI 응답이 토큰 제한으로 중단되었습니다.");
            }
            const content = choice.message.content;
            console.log("AI FINISH REASON:", choice.finish_reason);
            console.log("AI CONTENT:", content);
            if (typeof content !== "string" || !content.trim()) {
                throw new common_1.InternalServerErrorException("AI 응답이 비어있습니다.");
            }
            const jsonText = this.extractJson(content);
            const parsed = this.parseJson(jsonText);
            return this.validateResult(parsed);
        }
        catch (error) {
            console.error("AI 호출실패", error);
            if (error instanceof common_1.InternalServerErrorException) {
                throw error;
            }
            throw new common_1.InternalServerErrorException("AI 탐험 리포트 생성에 실패했습니다.");
        }
    }
    extractJson(content) {
        let text = content
            .trim()
            .replace(/^```json\s*/i, "")
            .replace(/^```\s*/i, "")
            .replace(/\s*```$/i, "")
            .trim();
        if (text.startsWith("{") && text.endsWith("}")) {
            return text;
        }
        const startIndex = text.indexOf("{");
        const endIndex = text.lastIndexOf("}");
        if (startIndex === -1 || endIndex === -1 || startIndex > endIndex) {
            throw new common_1.InternalServerErrorException("AI 응답에서 JSON을 찾을 수 없습니다.");
        }
        return text.slice(startIndex, endIndex + 1);
    }
    parseJson(jsonText) {
        try {
            return JSON.parse(jsonText);
        }
        catch (error) {
            console.error("AI JSON 파싱 실패");
            console.error("추출된 JSON:", jsonText);
            throw new common_1.InternalServerErrorException("AI 응답을 JSON으로 변환할 수 없습니다.");
        }
    }
    validateResult(data) {
        if (!data || typeof data !== "object") {
            throw new common_1.InternalServerErrorException("AI 응답 형식이 올바르지 않습니다.");
        }
        const result = data;
        if (typeof result.analysis !== "string" ||
            typeof result.reason !== "string") {
            console.error("잘못된 AI 응답:", result);
            throw new common_1.InternalServerErrorException("AI 응답에 analysis 또는 reason이 없습니다.");
        }
        return {
            analysis: result.analysis.trim(),
            reason: result.reason.trim(),
        };
    }
};
exports.AiClientService = AiClientService;
exports.AiClientService = AiClientService = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [])
], AiClientService);
//# sourceMappingURL=ai-client.service.js.map