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
                max_tokens: 300,
            });
            const content = response.choices[0]?.message?.content;
            if (!content) {
                throw new common_1.InternalServerErrorException("AI 응답이 비어있습니다.");
            }
            const result = JSON.parse(content);
            return result;
        }
        catch (error) {
            console.error("Gemma 호출실패", error);
            throw error;
        }
    }
};
exports.AiClientService = AiClientService;
exports.AiClientService = AiClientService = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [])
], AiClientService);
//# sourceMappingURL=ai-client.service.js.map